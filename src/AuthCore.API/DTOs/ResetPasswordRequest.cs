namespace AuthCore.API.DTOs;

public record ResetPasswordRequest(string Token, string NewPassword);
