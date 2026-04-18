using System.Security.Claims;
using AuthCore.API.Application.Interfaces;
using AuthCore.API.DTOs;
using AuthCore.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthCore.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.GeneralPolicy)]
[Produces("application/json")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lista todos os usuários. Requer role Admin.</summary>
    /// <response code="200">Lista de usuários.</response>
    /// <response code="403">Sem permissão de Admin.</response>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetAll()
    {
        var users = await userService.GetAllAsync();
        return Ok(users);
    }

    /// <summary>Retorna o perfil do usuário autenticado.</summary>
    /// <response code="200">Perfil do usuário.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), 200)]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetUserId();
        var user = await userService.GetMeAsync(userId);
        return Ok(user);
    }

    /// <summary>Altera a senha do usuário autenticado.</summary>
    /// <response code="204">Senha alterada.</response>
    /// <response code="400">Senha atual incorreta.</response>
    [HttpPatch("me/password")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetUserId();
        await userService.ChangePasswordAsync(userId, request);
        return NoContent();
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
