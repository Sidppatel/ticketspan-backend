namespace Db.Entities;

public class DeviceSession : BaseEntity
{
    public Guid UsersId { get; set; }
    public User User { get; set; } = null!;
    public string SessionHash { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
