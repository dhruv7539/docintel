namespace DocIntel.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "docintel";
    public string Audience { get; set; } = "docintel-clients";

    /// <summary>HMAC signing key. Override via configuration / secrets in any real deployment.</summary>
    public string Key { get; set; } = "dev-only-super-secret-signing-key-change-me-please-32+chars";

    public int ExpiryMinutes { get; set; } = 120;
}
