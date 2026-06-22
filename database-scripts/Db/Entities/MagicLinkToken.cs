namespace Db.Entities;

public class MagicLinkToken : BaseEntity
{
    public Guid? TenantsId { get; set; }
    public required string TokenHash { get; set; }
    public required string Email { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
}
