using AuthCore.API.DTOs;

namespace AuthCore.API.Application.Interfaces;

public interface IPasswordResetService
{
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
