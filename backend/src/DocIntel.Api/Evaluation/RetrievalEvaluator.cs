using DocIntel.Api.Ai;

namespace DocIntel.Api.Evaluation;

public record QueryCase(string Question, string Expected);

public record BucketReport(
    string Label,
    double Recall1,
    double Recall3,
    double Mrr,
    IReadOnlyList<(string Question, double Rr, string Top)> Rows);

/// <summary>
/// Reusable retrieval-quality harness: a labelled fixture run through the real
/// ranking pipeline (any <see cref="IEmbeddingClient"/> + cosine similarity) to
/// produce Recall@k and MRR. The corpus contains deliberate near-neighbour
/// documents (e.g. rollback vs canary, pooling vs caching, auth vs api keys) so
/// the adversarial queries actually test ranking precision rather than trivial
/// keyword lookup.
/// </summary>
public static class RetrievalEvaluator
{
    // filename -> document text
    public static readonly (string File, string Text)[] Corpus =
    {
        ("rollback.txt", "When a deployment fails its health checks, the Helm SDK performs an automatic rollback that brings mean time to recovery (MTTR) under 30 seconds, routing traffic back to the previous healthy release."),
        ("canary.txt", "A canary release rolls out a new version progressively to a small percentage of users first, and promotes it to everyone only after error rates stay healthy."),
        ("pooling.txt", "Database connection pooling reuses a fixed set of open connections instead of opening a new socket per request, improving throughput and latency under load."),
        ("caching.txt", "Frequently requested responses are cached in Redis with a short time-to-live, cutting database load and improving response times for repeated queries."),
        ("auth.txt", "Authentication uses JWT bearer tokens. User passwords are never stored in plaintext; they are hashed with BCrypt and a per-user salt so they stay safe even if the database leaks."),
        ("apikeys.txt", "Programmatic clients authenticate with API keys. Keys and other secrets are stored in a managed vault and rotated every ninety days, and a leaked key can be revoked instantly."),
        ("tenancy.txt", "Each workspace is an isolated tenant. Every document and query is scoped by workspace id, so one customer can never read another customer's files."),
        ("rbac.txt", "Role-based access control governs who may perform admin actions. Members are assigned roles such as viewer, editor, or admin, and permissions are checked on every request."),
        ("embedding.txt", "Uploaded documents are split into overlapping text chunks, and each chunk is embedded into a vector so it can be found later by semantic search."),
        ("ranking.txt", "At query time the question is embedded and compared to every stored chunk by cosine similarity; the most similar top-k passages are returned as grounding context."),
        ("backup.txt", "Nightly database backups are taken automatically and retained for thirty days, and any backup can be restored with a single command."),
        ("dr.txt", "For disaster recovery the service fails over to a standby region if the primary region becomes unavailable, with a recovery point objective of five minutes."),
        ("scaling.txt", "Horizontal autoscaling adds more API replicas automatically when CPU utilization exceeds seventy percent for two minutes, and removes them when demand falls."),
        ("loadbalancing.txt", "A load balancer spreads incoming requests across healthy instances, using health checks to decide which instances should receive traffic."),
        ("logging.txt", "Application logs are emitted as structured JSON and shipped to a central aggregator, tagged with a request id so a single request can be traced end to end."),
        ("metrics.txt", "Prometheus-style metrics are exported and charted on dashboards, and alerts page the on-call engineer when error rates or latency cross their thresholds."),
        ("ratelimit.txt", "Each workspace has a request quota; a rate limiter caps how many requests a tenant can make per minute and returns HTTP 429 when the quota is exceeded."),
        ("billing.txt", "Usage such as documents indexed and questions asked is metered per workspace and rolled up into a monthly invoice for usage-based billing."),
    };

    // Keyword queries share vocabulary with the target document.
    public static readonly QueryCase[] KeywordQueries =
    {
        new("how fast is rollback after a failed deployment", "rollback.txt"),
        new("configure database connection pooling for throughput", "pooling.txt"),
        new("how are user passwords stored", "auth.txt"),
        new("are document queries isolated per workspace tenant", "tenancy.txt"),
        new("how are documents split into chunks and embedded", "embedding.txt"),
        new("how long are nightly database backups retained", "backup.txt"),
        new("when does horizontal autoscaling add replicas", "scaling.txt"),
        new("where are structured logs shipped", "logging.txt"),
        new("how is a canary release rolled out progressively", "canary.txt"),
        new("how often are api keys rotated", "apikeys.txt"),
    };

    // Paraphrase queries express the same intent with different words (synonyms,
    // little or no shared vocabulary) — they require real semantics to match.
    public static readonly QueryCase[] ParaphraseQueries =
    {
        new("minimize downtime when a release breaks", "rollback.txt"),
        new("automatically promote a new build only if it stays healthy", "canary.txt"),
        new("reuse open handles instead of dialing out every call", "pooling.txt"),
        new("prevent data leakage between different organizations", "tenancy.txt"),
        new("add more compute automatically under heavy demand", "scaling.txt"),
        new("make stolen account credentials worthless to an attacker", "auth.txt"),
        new("pick the closest matching snippets for a query", "ranking.txt"),
        new("ship telemetry so we can chart trends and get paged", "metrics.txt"),
        new("stay online if a whole data center fails", "dr.txt"),
        new("serve frequent reads from memory instead of disk", "caching.txt"),
    };

    // Adversarial queries share salient words with a *distractor* document but
    // should still rank the correct one first — a precision test.
    public static readonly QueryCase[] AdversarialQueries =
    {
        new("how do health checks decide where to send traffic", "loadbalancing.txt"), // 'health checks'/'traffic' also in rollback.txt
        new("what happens to traffic during a bad deployment", "rollback.txt"),         // 'traffic' in loadbalancing, 'deployment' in canary
        new("how are secrets kept safe", "apikeys.txt"),                                 // vs auth.txt (passwords, 'safe')
        new("who is allowed to perform admin actions", "rbac.txt"),                      // vs auth/tenancy
        new("how is usage measured for charging customers", "billing.txt"),             // vs ratelimit/metrics ('usage','requests')
        new("how does caching cut response times", "caching.txt"),                       // vs pooling ('throughput','latency')
    };

    // Unanswerable queries — nothing in the corpus covers them. Used by the
    // answer-quality eval to check the model abstains instead of hallucinating.
    public static readonly QueryCase[] UnansweredQueries =
    {
        new("what is the company's parental leave policy", ""),
        new("who founded the parent company and when", ""),
        new("what fonts and colors does the marketing site use", ""),
        new("how do I export everything to a competitor's file format", ""),
    };

    public static async Task<IReadOnlyList<(string File, float[] Vector)>> EmbedCorpusAsync(
        IEmbeddingClient embeddings, CancellationToken ct = default)
    {
        var vectors = await embeddings.EmbedBatchAsync(Corpus.Select(c => c.Text).ToList(), ct);
        return Corpus.Select((c, i) => (c.File, vectors[i])).ToList();
    }

    public static async Task<BucketReport> EvaluateAsync(
        string label,
        IReadOnlyList<QueryCase> queries,
        IEmbeddingClient embeddings,
        IReadOnlyList<(string File, float[] Vector)> corpus,
        CancellationToken ct = default)
    {
        var recall1 = new List<double>();
        var recall3 = new List<double>();
        var rr = new List<double>();
        var rows = new List<(string, double, string)>();

        foreach (var (question, expected) in queries)
        {
            var q = await embeddings.EmbedAsync(question, ct);
            var ranked = corpus
                .Select(c => (c.File, Score: VectorMath.CosineSimilarity(q, c.Vector)))
                .OrderByDescending(x => x.Score)
                .Select(x => x.File)
                .ToList();

            var relevant = new HashSet<string> { expected };
            var thisRr = RetrievalMetrics.ReciprocalRank(ranked, relevant);

            recall1.Add(RetrievalMetrics.RecallAtK(ranked, relevant, 1));
            recall3.Add(RetrievalMetrics.RecallAtK(ranked, relevant, 3));
            rr.Add(thisRr);
            rows.Add((question, thisRr, ranked[0]));
        }

        return new BucketReport(
            label,
            RetrievalMetrics.Mean(recall1),
            RetrievalMetrics.Mean(recall3),
            RetrievalMetrics.Mean(rr),
            rows);
    }
}
