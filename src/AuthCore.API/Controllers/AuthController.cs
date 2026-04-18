using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Exceptions;
using AuthCore.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthCore.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController(IAuthService authService, IPasswordResetService passwordResetService, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshTokenCookie = "refreshToken";

    /// <summary>Registra um novo usuário com role User.</summary>
    /// <response code="201">Usuário criado. Retorna o access token e seta cookie refreshToken.</response>
    /// <response code="409">E-mail já cadastrado.</response>
    /// <response code="422">Dados inválidos.</response>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingExtensions.RegisterPolicy)]
    [ProducesResponseType(typeof(AuthResponse), 201)]
    [ProducesResponseType(409)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (response, rawRefreshToken) = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(rawRefreshToken);
        return Created(string.Empty, response);
    }

    /// <summary>Registra um novo usuário com role Admin. Requer autenticação Admin.</summary>
    /// <response code="201">Admin criado.</response>
    /// <response code="401">Não autenticado.</response>
    /// <response code="403">Sem permissão de Admin.</response>
    [HttpPost("register/admin")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting(RateLimitingExtensions.RegisterPolicy)]
    [ProducesResponseType(typeof(AuthResponse), 201)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterRequest request)
    {
        var (response, rawRefreshToken) = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent(), isAdmin: true);
        SetRefreshTokenCookie(rawRefreshToken);
        return Created(string.Empty, response);
    }

    /// <summary>Autentica o usuário e retorna um access token JWT.</summary>
    /// <response code="200">Login bem-sucedido. Retorna access token e seta cookie refreshToken.</response>
    /// <response code="401">Credenciais inválidas.</response>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (response, rawRefreshToken) = await authService.LoginAsync(request, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    /// <summary>Renova o access token usando o cookie refreshToken (rotation).</summary>
    /// <response code="200">Novo access token gerado.</response>
    /// <response code="401">Refresh token inválido, expirado ou reutilizado.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Refresh()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrEmpty(rawRefreshToken))
            throw new InvalidRefreshTokenException();

        var (response, newRawRefreshToken) = await authService.RefreshAsync(rawRefreshToken, GetIpAddress(), GetUserAgent());
        SetRefreshTokenCookie(newRawRefreshToken);
        return Ok(response);
    }

    /// <summary>Encerra a sessão revogando o refresh token.</summary>
    /// <response code="204">Logout realizado.</response>
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookie];
        if (!string.IsNullOrEmpty(rawRefreshToken))
            await authService.LogoutAsync(rawRefreshToken);

        ClearRefreshTokenCookie();
        return NoContent();
    }

    /// <summary>Solicita reset de senha. Retorna 200 mesmo se o e-mail não existir (anti-enumeração).</summary>
    /// <response code="200">Instrução enviada (ou silenciada se e-mail inexistente).</response>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitingExtensions.RegisterPolicy)]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await passwordResetService.ForgotPasswordAsync(request);
        return Ok(new { message = "If this email is registered, you will receive reset instructions." });
    }

    /// <summary>Redefine a senha usando o token de reset. Invalida todas as sessões ativas.</summary>
    /// <response code="204">Senha redefinida e sessões revogadas.</response>
    /// <response code="401">Token inválido, expirado ou já utilizado.</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await passwordResetService.ResetPasswordAsync(request);
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
