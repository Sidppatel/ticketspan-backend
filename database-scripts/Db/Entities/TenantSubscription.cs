namespace Db.Entities;

/// <summary>
/// A tenant's SaaS subscription (or 14-day trial). One active row per tenant at
/// most; history preserved as canceled/expired rows. Charges are recorded in
/// billing_charges; this row only tracks lifecycle state. Developer-managed.
/// </summary>
public class TenantSubscription : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>starter | professional | business | enterprise (trial rows use professional features).</summary>
    public required string Tier { get; set; }

    /// <summary>trial | active | past_due | canceled | expired.</summary>
    public string Status { get; set; } = "active";

    public int MonthlyPriceCents { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>When to downgrade to at period end (set on downgrade requests).</summary>
    public string? PendingTier { get; set; }

    public DateTime? TrialEndsAt { get; set; }
    /// <summary>Bitmask-free reminder tracking: last trial reminder day sent (10 or 13).</summary>
    public int TrialReminderDaySent { get; set; }

    public DateTime? CanceledAt { get; set; }
    public int FailedPaymentCount { get; set; }

    public string? StripeSubscriptionId { get; set; }
}
