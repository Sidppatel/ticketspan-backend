using Db.Enums;

namespace Db.Entities;

public class Invitation : BaseEntity
{
    public Guid? TenantsId { get; set; }
    public Tenant? Tenant { get; set; }
    public required string Email { get; set; }
    public required string TokenHash { get; set; }
    public short Role { get; set; }
    public Guid InvitedByUsersId { get; set; }
    public User InvitedBy { get; set; } = null!;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
