using DocIntel.Api.Ai;
using DocIntel.Api.Evaluation;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DocIntel.Tests;

/// <summary>
/// Offline retrieval-quality evaluation. Runs the labelled fixture through the
/// real ranking pipeline (StubEmbeddingClient + cosine similarity) and reports
/// Recall@k and MRR for keyword, paraphrase, and adversarial queries. Doubles as
/// a regression guard: the asserted thresholds fail the build if quality drops.
///
/// To see the metrics table, run:
///   dotnet test --logger "console;verbosity=detailed"
/// </summary>
public class RetrievalEvaluationTests
{
    private readonly ITestOutputHelper _output;

    public RetrievalEvaluationTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Retrieval_StubEmbedding_MeetsQualityThresholds()
    {
        var embeddings = new StubEmbeddingClient(256);
        var corpus = await RetrievalEvaluator.EmbedCorpusAsync(embeddings);
        var report = await EvaluateAllAsync("StubEmbeddingClient(256)", embeddings, corpus);

        // The stub leans on lexical overlap: strong on keyword, weaker on
        // paraphrase and adversarial (a real model lifts those — see opt-in test).
        Assert.True(report.Keyword.Recall3 >= 0.80, $"Keyword Recall@3 regressed: {report.Keyword.Recall3:0.000}");
        Assert.True(report.Keyword.Mrr >= 0.70, $"Keyword MRR regressed: {report.Keyword.Mrr:0.000}");
    }

    /// <summary>
    /// Live evaluation against a real OpenAI embedding model. Skipped unless
    /// OPENAI_API_KEY is set, so CI and offline runs are unaffected.
    /// </summary>
    [SkippableFact]
    public async Task Retrieval_OpenAiEmbedding_BeatsStubOnHardQueries()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "OPENAI_API_KEY not set; skipping live model evaluation.");

        var options = Options.Create(new AiOptions
        {
            Provider = "OpenAI",
            ApiKey = apiKey!,
            EmbeddingModel = "text-embedding-3-small",
        });
        using var http = new HttpClient();
        var embeddings = new OpenAiEmbeddingClient(http, options);

        var corpus = await RetrievalEvaluator.EmbedCorpusAsync(embeddings);
        var report = await EvaluateAllAsync("OpenAI text-embedding-3-small", embeddings, corpus);

        // A real embedding model handles paraphrase and adversarial distractors.
        Assert.True(report.Paraphrase.Mrr >= 0.80, $"Paraphrase MRR below expectation: {report.Paraphrase.Mrr:0.000}");
        Assert.True(report.Adversarial.Recall1 >= 0.66, $"Adversarial Recall@1 below expectation: {report.Adversarial.Recall1:0.000}");
    }

    private async Task<(BucketReport Keyword, BucketReport Paraphrase, BucketReport Adversarial)> EvaluateAllAsync(
        string model, IEmbeddingClient embeddings, IReadOnlyList<(string File, float[] Vector)> corpus)
    {
        var keyword = await RetrievalEvaluator.EvaluateAsync(
            "Keyword", RetrievalEvaluator.KeywordQueries, embeddings, corpus);
        var paraphrase = await RetrievalEvaluator.EvaluateAsync(
            "Paraphrase (semantic only)", RetrievalEvaluator.ParaphraseQueries, embeddings, corpus);
        var adversarial = await RetrievalEvaluator.EvaluateAsync(
            "Adversarial (distractor traps)", RetrievalEvaluator.AdversarialQueries, embeddings, corpus);

        foreach (var bucket in new[] { keyword, paraphrase, adversarial })
        {
            _output.WriteLine("");
            _output.WriteLine($"== {bucket.Label} ==");
            _output.WriteLine("Query                                                       | RR    | Top result");
            _output.WriteLine(new string('-', 90));
            foreach (var (question, rr, top) in bucket.Rows)
            {
                _output.WriteLine($"{question,-59} | {rr:0.00}  | {top}");
            }
        }

        _output.WriteLine("");
        _output.WriteLine($"Corpus: {RetrievalEvaluator.Corpus.Length} docs   Embedding: {model}");
        _output.WriteLine($"  Keyword     -> Recall@1 {keyword.Recall1:0.000}  Recall@3 {keyword.Recall3:0.000}  MRR {keyword.Mrr:0.000}");
        _output.WriteLine($"  Paraphrase  -> Recall@1 {paraphrase.Recall1:0.000}  Recall@3 {paraphrase.Recall3:0.000}  MRR {paraphrase.Mrr:0.000}");
        _output.WriteLine($"  Adversarial -> Recall@1 {adversarial.Recall1:0.000}  Recall@3 {adversarial.Recall3:0.000}  MRR {adversarial.Mrr:0.000}");

        return (keyword, paraphrase, adversarial);
    }
}

public class RetrievalMetricsTests
{
    private static readonly string[] Ranked = { "a", "b", "c", "d" };

    [Fact]
    public void RecallAtK_CountsRelevantInTopK()
    {
        var relevant = new HashSet<string> { "c" };
        Assert.Equal(0d, RetrievalMetrics.RecallAtK(Ranked, relevant, 2));
        Assert.Equal(1d, RetrievalMetrics.RecallAtK(Ranked, relevant, 3));
    }

    [Fact]
    public void RecallAtK_PartialWhenMultipleRelevant()
    {
        var relevant = new HashSet<string> { "a", "d" };
        Assert.Equal(0.5d, RetrievalMetrics.RecallAtK(Ranked, relevant, 2));
    }

    [Fact]
    public void ReciprocalRank_UsesFirstRelevantPosition()
    {
        Assert.Equal(1d, RetrievalMetrics.ReciprocalRank(Ranked, new HashSet<string> { "a" }));
        Assert.Equal(0.5d, RetrievalMetrics.ReciprocalRank(Ranked, new HashSet<string> { "b" }));
        Assert.Equal(0d, RetrievalMetrics.ReciprocalRank(Ranked, new HashSet<string> { "z" }));
    }

    [Fact]
    public void Mean_OfEmpty_IsZero()
    {
        Assert.Equal(0d, RetrievalMetrics.Mean(Array.Empty<double>()));
    }
}
