namespace AuthCore.API.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
