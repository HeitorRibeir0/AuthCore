using AuthCore.API.DTOs;

namespace AuthCore.API.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<UserResponse> GetMeAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}
