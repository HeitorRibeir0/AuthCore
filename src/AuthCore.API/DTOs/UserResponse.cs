namespace AuthCore.API.DTOs;

public record UserResponse(Guid Id, string Name, string Email, string Role, DateTime CreatedAt);
