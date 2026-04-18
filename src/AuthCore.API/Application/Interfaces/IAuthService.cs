using AuthCore.API.DTOs;

namespace AuthCore.API.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent);
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent);
}
