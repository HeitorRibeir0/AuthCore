using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Entities;
using AuthCore.API.Exceptions;
using AuthCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthCore.API.Application.Services;

public class PasswordResetService(
    AppDbContext db,
    ITokenService tokenService,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Sempre retorna 200 para não revelar se o e-mail existe
        if (user is null)
        {
            logger.LogInformation("Password reset requested for unknown email {Email}", request.Email);
            return;
        }

        var rawToken = tokenService.GenerateRefreshToken();
        var tokenHash = tokenService.HashToken(rawToken);

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        db.PasswordResetTokens.Add(resetToken);
        await db.SaveChangesAsync();

        // Em produção: enviar por e-mail. Aqui logamos para desenvolvimento.
        logger.LogInformation("Password reset token for user {UserId}: {Token}", user.Id, rawToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var tokenHash = tokenService.HashToken(request.Token);

        var resetToken = await db.PasswordResetTokens
            .Include(prt => prt.User)
            .FirstOrDefaultAsync(prt => prt.TokenHash == tokenHash);

        if (resetToken is null)
            throw new InvalidResetTokenException();

        if (resetToken.UsedAt is not null)
            throw new InvalidResetTokenException();

        if (resetToken.ExpiresAt < DateTime.UtcNow)
            throw new InvalidResetTokenException();

        // Marca como usado (auditoria) — evita replay attack
        resetToken.UsedAt = DateTime.UtcNow;

        // Troca a senha
        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        // Revoga todas as sessões ativas — senha comprometida não pode manter sessões abertas
        var activeTokens = await db.RefreshTokens
            .Where(rt => rt.UserId == resetToken.UserId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
            token.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation("Password reset completed for user {UserId}. {Count} sessions revoked.",
            resetToken.UserId, activeTokens.Count);
    }
}
