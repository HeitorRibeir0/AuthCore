using AuthCore.API.DTOs;

namespace AuthCore.API.Application.Interfaces;

public interface IAuthService
{
    Task<(AuthResponse Response, string RawRefreshToken)> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent, bool isAdmin = false);
    Task<(AuthResponse Response, string RawRefreshToken)> LoginAsync(LoginRequest request, string ipAddress, string userAgent);
    Task<(AuthResponse Response, string RawRefreshToken)> RefreshAsync(string rawRefreshToken, string ipAddress, string userAgent);
    Task LogoutAsync(string rawRefreshToken);
}
