namespace Db.Entities;

public class PasswordResetToken : BaseEntity
{
    public Guid UsersId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
}
