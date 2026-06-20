using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocIntel.Api.Ai;

namespace DocIntel.Api.Evaluation;

public record JudgeScore(int Groundedness, int Relevance, bool Abstained, string Rationale);

public record AnswerJudgement(
    string Question, string Answer, int Groundedness, int Relevance, bool Abstained, string Rationale);

public record AnswerQualityReport(
    double AvgGroundedness,
    double AvgRelevance,
    double GroundedRate,
    double AbstentionRate,
    IReadOnlyList<AnswerJudgement> Rows);

/// <summary>Scores a generated answer against the context it was given.</summary>
public interface IAnswerJudge
{
    Task<JudgeScore> JudgeAsync(string question, string context, string answer, CancellationToken ct = default);
}

/// <summary>
/// End-to-end answer-quality harness: runs the full RAG pipeline (retrieve top-k
/// with <paramref name="embeddings"/>, answer with <paramref name="answerer"/>)
/// for each query, then has an <see cref="IAnswerJudge"/> rate groundedness
/// (is the answer supported by the retrieved context?), relevance, and whether
/// the model abstained (declined for lack of context).
/// </summary>
public static class AnswerQualityEvaluator
{
    public static async Task<AnswerQualityReport> EvaluateAsync(
        IReadOnlyList<QueryCase> queries,
        IEmbeddingClient embeddings,
        ILlmClient answerer,
        IAnswerJudge judge,
        int topK = 4,
        CancellationToken ct = default)
    {
        var corpus = await RetrievalEvaluator.EmbedCorpusAsync(embeddings, ct);
        var rows = new List<AnswerJudgement>();

        foreach (var (question, _) in queries)
        {
            var q = await embeddings.EmbedAsync(question, ct);
            var top = corpus
                .Select(c => (c.File, Score: VectorMath.CosineSimilarity(q, c.Vector)))
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .ToList();

            var context = top
                .Select((r, i) => new RetrievedContext(
                    r.File, i, RetrievalEvaluator.Corpus.First(d => d.File == r.File).Text, r.Score))
                .ToList();

            var answer = await answerer.GenerateAnswerAsync(question, context, ct);
            var contextText = string.Join("\n", context.Select(c => $"[{c.FileName} #{c.Ordinal}] {c.Content}"));
            var s = await judge.JudgeAsync(question, contextText, answer, ct);
            rows.Add(new AnswerJudgement(question, answer, s.Groundedness, s.Relevance, s.Abstained, s.Rationale));
        }

        return new AnswerQualityReport(
            RetrievalMetrics.Mean(rows.Select(r => (double)r.Groundedness)),
            RetrievalMetrics.Mean(rows.Select(r => (double)r.Relevance)),
            rows.Count == 0 ? 0d : (double)rows.Count(r => r.Groundedness >= 4) / rows.Count,
            rows.Count == 0 ? 0d : (double)rows.Count(r => r.Abstained) / rows.Count,
            rows);
    }
}

/// <summary>
/// LLM-as-judge backed by OpenAI chat completions. Asks the model to score an
/// answer on a 1-5 rubric and return strict JSON. Use a stronger model than the
/// answerer (and a different one) to reduce self-evaluation bias.
/// </summary>
public class OpenAiAnswerJudge : IAnswerJudge
{
    private const string System =
        "You are a strict evaluator of retrieval-augmented answers. You are given a QUESTION, " +
        "the CONTEXT passages that were provided to an assistant, and the assistant's ANSWER. " +
        "Score two dimensions from 1 to 5 and detect abstention:\n" +
        "- groundedness: are ALL claims in the answer supported by the context? 5 = fully supported, " +
        "1 = mostly fabricated. If the context does not contain the answer and the assistant correctly " +
        "says it cannot answer, that is grounded (5).\n" +
        "- relevance: does the answer address the question? 5 = directly answers (a correct refusal counts), " +
        "1 = off-topic.\n" +
        "- abstained: true if the answer declines or says the context is insufficient, otherwise false.\n" +
        "Respond with ONLY a JSON object: " +
        "{\"groundedness\":<int>,\"relevance\":<int>,\"abstained\":<bool>,\"rationale\":\"<one short sentence>\"}.";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiAnswerJudge(HttpClient http, string apiKey, string model = "gpt-4.1")
    {
        _http = http;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<JudgeScore> JudgeAsync(
        string question, string context, string answer, CancellationToken ct = default)
    {
        var user = $"QUESTION:\n{question}\n\nCONTEXT:\n{context}\n\nANSWER:\n{answer}";

        var payload = new
        {
            model = _model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = System },
                new { role = "user", content = user }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement
            .GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

        using var scored = JsonDocument.Parse(content);
        var root = scored.RootElement;
        return new JudgeScore(
            ReadInt(root, "groundedness"),
            ReadInt(root, "relevance"),
            root.TryGetProperty("abstained", out var a) && a.ValueKind is JsonValueKind.True,
            root.TryGetProperty("rationale", out var r) ? r.GetString() ?? "" : "");
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return 0;
        }

        return el.ValueKind == JsonValueKind.Number ? el.GetInt32()
            : int.TryParse(el.GetString(), out var v) ? v : 0;
    }
}
