namespace Db.Entities;

/// <summary>
/// A provisioned premium add-on for a tenant. Recurring price is charged per
/// period into billing_charges. Usage counters (SMS sent, domains configured)
/// live on this row. Developer-managed.
/// </summary>
public class TenantAddon : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>custom_domain | advanced_analytics | sms | extra_manager.</summary>
    public required string Type { get; set; }

    /// <summary>monthly | annual.</summary>
    public string BillingPeriod { get; set; } = "monthly";

    /// <summary>Seats for extra_manager, domains for custom_domain; 1 otherwise.</summary>
    public int Quantity { get; set; } = 1;

    public int PriceCents { get; set; }
    public int SetupFeeCents { get; set; }

    /// <summary>active | canceled.</summary>
    public string Status { get; set; } = "active";

    public DateTime CurrentPeriodEnd { get; set; }
    public int UsageCount { get; set; }
    public DateTime? CanceledAt { get; set; }
}
