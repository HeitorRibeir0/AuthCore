using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Exceptions;
using AuthCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthCore.API.Application.Services;

public class UserService(AppDbContext db) : IUserService
{
    public async Task<IEnumerable<UserResponse>> GetAllAsync()
    {
        return await db.Users
            .Select(u => new UserResponse(u.Id, u.Name, u.Email, u.Role.ToString(), u.CreatedAt))
            .ToListAsync();
    }

    public async Task<UserResponse> GetMeAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserResponse(user.Id, user.Name, user.Email, user.Role.ToString(), user.CreatedAt);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidPasswordException();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
    }
}
