using AuthCore.API.Application.Services;
using AuthCore.API.DTOs;
using AuthCore.API.Entities;
using AuthCore.API.Enums;
using AuthCore.API.Exceptions;
using AuthCore.API.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AuthCore.Tests.Unit;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<API.Application.Interfaces.ITokenService> _tokenService;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _tokenService = new Mock<API.Application.Interfaces.ITokenService>();
        var logger = new Mock<ILogger<AuthService>>();

        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh-token");
        _tokenService.Setup(t => t.HashToken(It.IsAny<string>())).Returns<string>(t => $"hash:{t}");

        _sut = new AuthService(_db, _tokenService.Object, logger.Object);
    }

    // ── Register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        var request = new RegisterRequest("Alice", "alice@example.com", "Password1");

        await _sut.RegisterAsync(request, "127.0.0.1", "test-agent");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "alice@example.com");
        user.Should().NotBeNull();
        user!.Role.Should().Be(Role.User);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ThrowsEmailAlreadyExistsException()
    {
        var request = new RegisterRequest("Alice", "alice@example.com", "Password1");
        await _sut.RegisterAsync(request, "127.0.0.1", "agent");

        var act = () => _sut.RegisterAsync(request, "127.0.0.1", "agent");

        await act.Should().ThrowAsync<EmailAlreadyExistsException>();
    }

    [Fact]
    public async Task Register_DuplicateEmail_DoesNotCreateSecondUser()
    {
        var request = new RegisterRequest("Alice", "alice@example.com", "Password1");
        await _sut.RegisterAsync(request, "127.0.0.1", "agent");

        try { await _sut.RegisterAsync(request, "127.0.0.1", "agent"); } catch { }

        var count = await _db.Users.CountAsync(u => u.Email == "alice@example.com");
        count.Should().Be(1);
    }

    [Fact]
    public async Task Register_AsAdmin_SetsAdminRole()
    {
        var request = new RegisterRequest("Admin", "admin@example.com", "Password1");

        await _sut.RegisterAsync(request, "127.0.0.1", "agent", isAdmin: true);

        var user = await _db.Users.FirstAsync(u => u.Email == "admin@example.com");
        user.Role.Should().Be(Role.Admin);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");

        var (response, rawRefresh) = await _sut.LoginAsync(new LoginRequest("alice@example.com", "Password1"), "ip", "ua");

        response.AccessToken.Should().Be("access-token");
        rawRefresh.Should().Be("raw-refresh-token");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsInvalidCredentialsException()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");

        var act = () => _sut.LoginAsync(new LoginRequest("alice@example.com", "WrongPass1"), "ip", "ua");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ThrowsSameExceptionAsWrongPassword()
    {
        var act = () => _sut.LoginAsync(new LoginRequest("ghost@example.com", "Password1"), "ip", "ua");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokensAndRevokesOld()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");
        var rawToken = "raw-refresh-token";

        var (response, newRaw) = await _sut.RefreshAsync(rawToken, "ip", "ua");

        response.AccessToken.Should().Be("access-token");
        var old = await _db.RefreshTokens.FirstAsync(rt => rt.TokenHash == "hash:raw-refresh-token");
        old.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ThrowsInvalidRefreshTokenException()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");
        var token = await _db.RefreshTokens.FirstAsync();
        token.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var act = () => _sut.RefreshAsync("raw-refresh-token", "ip", "ua");

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesAllAndThrows()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");
        var token = await _db.RefreshTokens.FirstAsync();
        token.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var act = () => _sut.RefreshAsync("raw-refresh-token", "ip", "ua");

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        var active = await _db.RefreshTokens.Where(rt => rt.RevokedAt == null).CountAsync();
        active.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ThrowsInvalidRefreshTokenException()
    {
        var act = () => _sut.RefreshAsync("nonexistent-token", "ip", "ua");

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithValidToken_RevokesToken()
    {
        await _sut.RegisterAsync(new RegisterRequest("Alice", "alice@example.com", "Password1"), "ip", "ua");

        await _sut.LogoutAsync("raw-refresh-token");

        var token = await _db.RefreshTokens.FirstAsync();
        token.RevokedAt.Should().NotBeNull();
    }

    public void Dispose() => _db.Dispose();
}
