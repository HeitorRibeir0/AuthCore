using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Enums;
using AuthCore.API.Entities;
using AuthCore.API.Exceptions;
using AuthCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthCore.API.Application.Services;

public class AuthService(AppDbContext db, ITokenService tokenService, ILogger<AuthService> logger) : IAuthService
{
    public async Task<(AuthResponse Response, string RawRefreshToken)> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent, bool isAdmin = false)
    {
        var emailTaken = await db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
            throw new EmailAlreadyExistsException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = isAdmin ? Role.Admin : Role.User
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await CreateAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<(AuthResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return await CreateAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<(AuthResponse Response, string RawRefreshToken)> RefreshAsync(string rawRefreshToken, string ipAddress, string userAgent)
    {
        var tokenHash = tokenService.HashToken(rawRefreshToken);

        var storedToken = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (storedToken is null)
            throw new InvalidRefreshTokenException();

        // Reuse detection: token já foi revogado → possível roubo, invalida tudo
        if (storedToken.RevokedAt is not null)
        {
            logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all tokens.", storedToken.UserId);
            await RevokeAllUserTokensAsync(storedToken.UserId);
            throw new InvalidRefreshTokenException();
        }

        if (storedToken.ExpiresAt < DateTime.UtcNow)
            throw new InvalidRefreshTokenException();

        storedToken.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await CreateAuthResponseAsync(storedToken.User, ipAddress, userAgent);
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        var tokenHash = tokenService.HashToken(rawRefreshToken);

        var storedToken = await db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash && rt.RevokedAt == null);

        if (storedToken is null)
            return;

        storedToken.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<(AuthResponse Response, string RawRefreshToken)> CreateAuthResponseAsync(User user, string ipAddress, string userAgent)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var rawRefreshToken = tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenService.HashToken(rawRefreshToken),
            UserId = user.Id,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return (new AuthResponse(accessToken), rawRefreshToken);
    }

    private async Task RevokeAllUserTokensAsync(Guid userId)
    {
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
            token.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
