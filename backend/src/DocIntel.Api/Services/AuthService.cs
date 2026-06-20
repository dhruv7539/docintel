using DocIntel.Api.Auth;
using DocIntel.Api.Dtos;
using DocIntel.Api.Models;
using DocIntel.Api.Repositories;

namespace DocIntel.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

/// <summary>
/// Handles registration and login. Registration provisions a brand-new
/// workspace (tenant) and its first admin user; login authenticates against the
/// stored BCrypt hash and mints a workspace-scoped JWT.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IWorkspaceRepository _workspaces;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(
        IUserRepository users,
        IWorkspaceRepository workspaces,
        IPasswordHasher hasher,
        ITokenService tokens)
    {
        _users = users;
        _workspaces = workspaces;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _users.FindByEmailAsync(request.Email, ct);
        if (existing is not null)
        {
            throw new ConflictException("An account with that email already exists.");
        }

        var workspace = new Workspace { Name = request.WorkspaceName };
        await _workspaces.AddAsync(workspace, ct);

        var user = new User
        {
            WorkspaceId = workspace.Id,
            Email = request.Email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = "admin"
        };
        await _users.AddAsync(user, ct);

        return new AuthResponse(_tokens.CreateToken(user), user.Email, workspace.Id, workspace.Name);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException("Invalid email or password.", 401);
        }

        var workspaceName = user.Workspace?.Name ?? string.Empty;
        return new AuthResponse(_tokens.CreateToken(user), user.Email, user.WorkspaceId, workspaceName);
    }
}
