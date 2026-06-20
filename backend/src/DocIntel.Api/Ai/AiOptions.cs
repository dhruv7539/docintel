namespace DocIntel.Api.Ai;

/// <summary>
/// Binds the "Ai" configuration section. When <see cref="Provider"/> is "Stub"
/// (the default) the service runs fully offline with deterministic fakes, which
/// keeps local dev and the test suite free of API keys. Set it to "AzureOpenAI"
/// or "OpenAI" and supply credentials to use a real model.
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"Stub" | "AzureOpenAI" | "OpenAI".</summary>
    public string Provider { get; set; } = "Stub";

    // Shared.
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 256;

    // Azure OpenAI specific.
    public string Endpoint { get; set; } = string.Empty;
    public string ChatDeployment { get; set; } = string.Empty;
    public string EmbeddingDeployment { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-06-01";
}
