using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthCore.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await authService.RegisterAsync(request, GetIpAddress(), GetUserAgent());
        return Created(string.Empty, response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await authService.LoginAsync(request, GetIpAddress(), GetUserAgent());
        return Ok(response);
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetUserAgent() =>
        Request.Headers.UserAgent.ToString();
}
