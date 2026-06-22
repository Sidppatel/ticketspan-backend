namespace Db.Entities.Views;

public class DeviceSessionView
{
    public Guid DeviceSessionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BusinessUserId { get; set; }
    public string SessionHash { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
