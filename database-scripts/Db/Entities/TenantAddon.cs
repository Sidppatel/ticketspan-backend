namespace Db.Entities;

public class TenantAddon : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public required string Type { get; set; }

    public string BillingPeriod { get; set; } = "monthly";

    public int Quantity { get; set; } = 1;

    public int PriceCents { get; set; }
    public int SetupFeeCents { get; set; }

    public string Status { get; set; } = "active";

    public DateTime CurrentPeriodEnd { get; set; }
    public int UsageCount { get; set; }
    public DateTime? CanceledAt { get; set; }
}
