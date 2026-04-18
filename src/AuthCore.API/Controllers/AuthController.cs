using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshTokenCookie = "refreshToken";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (response, rawRefreshToken) = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(rawRefreshToken);
        return Created(string.Empty, response);
    }

    [HttpPost("register/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterRequest request)
    {
        var (response, rawRefreshToken) = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent(), isAdmin: true);
        SetRefreshTokenCookie(rawRefreshToken);
        return Created(string.Empty, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (response, rawRefreshToken) = await authService.LoginAsync(request, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawRefreshToken))
            throw new InvalidRefreshTokenException();

        var (response, newRawRefreshToken) = await authService.RefreshAsync(rawRefreshToken, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(newRawRefreshToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(rawRefreshToken))
            await authService.LogoutAsync(rawRefreshToken);

        ClearRefreshTokenCookie();
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append(RefreshTokenCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict
        });
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        Request.Headers.UserAgent.ToString();
}
