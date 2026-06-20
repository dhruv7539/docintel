using DocIntel.Api.Ai;
using DocIntel.Api.Evaluation;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DocIntel.Tests;

public class AnswerQualityEvaluationTests
{
    private readonly ITestOutputHelper _output;

    public AnswerQualityEvaluationTests(ITestOutputHelper output) => _output = output;

    /// <summary>Offline wiring check: the evaluator aggregates judge scores correctly, no network.</summary>
    [Fact]
    public async Task Evaluator_AggregatesJudgeScores()
    {
        var report = await AnswerQualityEvaluator.EvaluateAsync(
            RetrievalEvaluator.KeywordQueries,
            new StubEmbeddingClient(256),
            new StubLlmClient(),
            new FixedJudge(new JudgeScore(5, 4, false, "fixed")));

        Assert.Equal(RetrievalEvaluator.KeywordQueries.Length, report.Rows.Count);
        Assert.Equal(5d, report.AvgGroundedness);
        Assert.Equal(4d, report.AvgRelevance);
        Assert.Equal(1d, report.GroundedRate);    // all >= 4
        Assert.Equal(0d, report.AbstentionRate);   // none abstained
    }

    /// <summary>
    /// Live answer-quality evaluation. Generates answers with gpt-4o-mini and
    /// judges them with a stronger, different model (gpt-4.1 by default, override
    /// with JUDGE_MODEL) to reduce self-evaluation bias. Skipped unless
    /// OPENAI_API_KEY is set. Run with:
    ///   OPENAI_API_KEY=sk-... dotnet test --filter AnswerQuality --logger "console;verbosity=detailed"
    /// </summary>
    [SkippableFact]
    public async Task AnswerQuality_OpenAi_GroundedOnHardSetAndAbstainsWhenUnanswerable()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Skip.If(string.IsNullOrWhiteSpace(apiKey), "OPENAI_API_KEY not set; skipping answer-quality evaluation.");

        var judgeModel = Environment.GetEnvironmentVariable("JUDGE_MODEL") ?? "gpt-4.1";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var embeddings = new OpenAiEmbeddingClient(http, Options.Create(new AiOptions
        {
            Provider = "OpenAI", ApiKey = apiKey!, EmbeddingModel = "text-embedding-3-small",
        }));
        var answerer = new OpenAiLlmClient(http, Options.Create(new AiOptions
        {
            Provider = "OpenAI", ApiKey = apiKey!, ChatModel = "gpt-4o-mini",
        }));
        var judge = new OpenAiAnswerJudge(http, apiKey!, judgeModel);

        // Hard answerable set: paraphrase + adversarial (the easy keyword queries
        // are covered by retrieval; here we stress generation).
        var hard = RetrievalEvaluator.ParaphraseQueries
            .Concat(RetrievalEvaluator.AdversarialQueries)
            .ToList();

        var answerable = await AnswerQualityEvaluator.EvaluateAsync(hard, embeddings, answerer, judge);
        var unanswerable = await AnswerQualityEvaluator.EvaluateAsync(
            RetrievalEvaluator.UnansweredQueries, embeddings, answerer, judge);

        Print("Hard answerable (paraphrase + adversarial)", answerable);
        Print("Unanswerable (should abstain)", unanswerable);

        _output.WriteLine("");
        _output.WriteLine($"Answerer: gpt-4o-mini   Judge: {judgeModel}");
        _output.WriteLine($"  Answerable  -> groundedness {answerable.AvgGroundedness:0.00}/5  relevance {answerable.AvgRelevance:0.00}/5  grounded {answerable.GroundedRate:0.0%}");
        _output.WriteLine($"  Unanswerable-> abstained {unanswerable.AbstentionRate:0.0%}  groundedness {unanswerable.AvgGroundedness:0.00}/5");

        // Answers should be grounded and on-topic; unanswerable questions should be
        // declined rather than answered (anti-hallucination).
        Assert.True(answerable.AvgGroundedness >= 4.0, $"Groundedness below bar: {answerable.AvgGroundedness:0.00}");
        Assert.True(answerable.AvgRelevance >= 4.0, $"Relevance below bar: {answerable.AvgRelevance:0.00}");
        Assert.True(unanswerable.AbstentionRate >= 0.75, $"Abstention too low: {unanswerable.AbstentionRate:0.0%}");
    }

    private void Print(string title, AnswerQualityReport report)
    {
        _output.WriteLine("");
        _output.WriteLine($"== {title} ==");
        _output.WriteLine("Question                                                     | Grnd | Rel | Abst | Rationale");
        _output.WriteLine(new string('-', 120));
        foreach (var r in report.Rows)
        {
            _output.WriteLine($"{r.Question,-59} |  {r.Groundedness}   |  {r.Relevance}  |  {(r.Abstained ? "Y" : "n")}   | {r.Rationale}");
        }
    }

    private sealed class FixedJudge : IAnswerJudge
    {
        private readonly JudgeScore _score;
        public FixedJudge(JudgeScore score) => _score = score;

        public Task<JudgeScore> JudgeAsync(
            string question, string context, string answer, CancellationToken ct = default)
            => Task.FromResult(_score);
    }
}
