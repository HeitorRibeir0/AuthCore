using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Entities;
using AuthCore.API.Exceptions;
using AuthCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthCore.API.Application.Services;

public class AuthService(AppDbContext db, ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent)
    {
        var emailTaken = await db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailTaken)
            throw new EmailAlreadyExistsException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await CreateAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return await CreateAuthResponseAsync(user, ipAddress, userAgent);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, string ipAddress, string userAgent)
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

        return new AuthResponse(accessToken, rawRefreshToken);
    }
}
