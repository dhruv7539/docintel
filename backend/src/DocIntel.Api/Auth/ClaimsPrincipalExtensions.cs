using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace DocIntel.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Resolves the caller's tenant (workspace) id from the JWT. This is the
    /// anchor for multi-tenant isolation: controllers pass it into every
    /// service call so a user can never read another workspace's data.
    /// </summary>
    public static Guid GetWorkspaceId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtTokenService.WorkspaceClaim);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing workspace claim.");
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing subject claim.");
    }
}
