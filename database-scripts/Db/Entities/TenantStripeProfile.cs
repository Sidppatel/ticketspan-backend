namespace Db.Entities;

public class TenantStripeProfile
{
    public Guid TenantsId { get; set; }
    public Tenant? Tenant { get; set; }

    public string? BusinessType { get; set; }
    public string? BusinessUrl { get; set; }
    public string? ProductDescription { get; set; }
    public string? Mcc { get; set; }
    public string? SupportEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
