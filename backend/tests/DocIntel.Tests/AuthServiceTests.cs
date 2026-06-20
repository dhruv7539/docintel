using DocIntel.Api.Auth;
using DocIntel.Api.Dtos;
using DocIntel.Api.Models;
using DocIntel.Api.Repositories;
using DocIntel.Api.Services;
using Moq;
using Xunit;

namespace DocIntel.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IWorkspaceRepository> _workspaces = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();

    private AuthService CreateSut() =>
        new(_users.Object, _workspaces.Object, _hasher.Object, _tokens.Object);

    [Fact]
    public async Task Register_NewEmail_CreatesWorkspaceAndUser()
    {
        _users.Setup(r => r.FindByEmailAsync("new@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _hasher.Setup(h => h.Hash("password123")).Returns("hashed");
        _tokens.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("jwt-token");

        var sut = CreateSut();
        var result = await sut.RegisterAsync(new RegisterRequest("Acme", "new@x.com", "password123"));

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal("Acme", result.WorkspaceName);
        _workspaces.Verify(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.AddAsync(It.Is<User>(u => u.PasswordHash == "hashed" && u.Role == "admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        _users.Setup(r => r.FindByEmailAsync("dup@x.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "dup@x.com" });

        var sut = CreateSut();
        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.RegisterAsync(new RegisterRequest("Acme", "dup@x.com", "password123")));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var workspace = new Workspace { Name = "Acme" };
        var user = new User
        {
            Email = "u@x.com",
            PasswordHash = "hashed",
            WorkspaceId = workspace.Id,
            Workspace = workspace
        };
        _users.Setup(r => r.FindByEmailAsync("u@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("password123", "hashed")).Returns(true);
        _tokens.Setup(t => t.CreateToken(user)).Returns("jwt-token");

        var sut = CreateSut();
        var result = await sut.LoginAsync(new LoginRequest("u@x.com", "password123"));

        Assert.Equal("jwt-token", result.Token);
        Assert.Equal(workspace.Id, result.WorkspaceId);
    }

    [Fact]
    public async Task Login_WrongPassword_Throws()
    {
        var user = new User { Email = "u@x.com", PasswordHash = "hashed" };
        _users.Setup(r => r.FindByEmailAsync("u@x.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.LoginAsync(new LoginRequest("u@x.com", "wrong")));
        Assert.Equal(401, ex.StatusCode);
    }
}
