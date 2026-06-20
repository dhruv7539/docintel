using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DocIntel.Api.Ai;

/// <summary>
/// Real chat-completions client that talks to either Azure OpenAI or OpenAI,
/// selected by <see cref="AiOptions.Provider"/>. Wired up only when a non-stub
/// provider is configured; otherwise <see cref="StubLlmClient"/> is used.
/// </summary>
public class OpenAiLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;

    public OpenAiLlmClient(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GenerateAnswerAsync(
        string question,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken ct = default)
    {
        var contextBlock = new StringBuilder();
        foreach (var c in context)
        {
            contextBlock.AppendLine($"[{c.FileName} #{c.Ordinal}] {c.Content}");
        }

        var system =
            "You are a document intelligence assistant. Answer the user's question using ONLY the " +
            "provided context. Cite sources as [file #ordinal]. If the context is insufficient, say so.";

        var userMsg = $"Context:\n{contextBlock}\n\nQuestion: {question}";

        var payload = new ChatRequest
        {
            Model = _options.ChatModel,
            Messages = new[]
            {
                new ChatMessage { Role = "system", Content = system },
                new ChatMessage { Role = "user", Content = userMsg }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatUri());
        ApplyAuth(request);
        request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        return result?.Choices?.FirstOrDefault()?.Message?.Content
               ?? "No answer was returned by the model.";
    }

    private string BuildChatUri() => _options.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
        ? $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.ChatDeployment}/chat/completions?api-version={_options.ApiVersion}"
        : "https://api.openai.com/v1/chat/completions";

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (_options.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("api-key", _options.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private class ChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("messages")] public ChatMessage[] Messages { get; set; } = Array.Empty<ChatMessage>();
        [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.2;
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
    }

    private class ChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
    }
}

/// <summary>Real embeddings client for Azure OpenAI / OpenAI.</summary>
public class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;

    public OpenAiEmbeddingClient(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = await EmbedBatchAsync(new[] { text }, ct);
        return batch.Count > 0 ? batch[0] : Array.Empty<float>();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var payload = new EmbeddingRequest
        {
            Model = _options.EmbeddingModel,
            Input = texts.ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildEmbeddingUri());
        ApplyAuth(request);
        request.Content = JsonContent.Create(payload);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: ct);
        return result?.Data?
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding ?? Array.Empty<float>())
            .ToList() ?? new List<float[]>();
    }

    private string BuildEmbeddingUri() => _options.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
        ? $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.EmbeddingDeployment}/embeddings?api-version={_options.ApiVersion}"
        : "https://api.openai.com/v1/embeddings";

    private void ApplyAuth(HttpRequestMessage request)
    {
        if (_options.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("api-key", _options.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private class EmbeddingRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("input")] public string[] Input { get; set; } = Array.Empty<string>();
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")] public List<EmbeddingData>? Data { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("index")] public int Index { get; set; }
        [JsonPropertyName("embedding")] public float[]? Embedding { get; set; }
    }
}
