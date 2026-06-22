using Db.Enums;

namespace Db.Entities.Views;

public class InvitationView
{
    public Guid InvitationId { get; set; }
    public Guid? TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public short Role { get; set; }
    public Guid InvitedByUserId { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string InviterFirstName { get; set; } = string.Empty;
    public string InviterLastName { get; set; } = string.Empty;
}
