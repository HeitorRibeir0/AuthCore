namespace AuthCore.API.DTOs;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
