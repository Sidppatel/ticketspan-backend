namespace Db.Entities;

public class TenantSubscription : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public required string Tier { get; set; }

    public string Status { get; set; } = "active";

    public int MonthlyPriceCents { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    public string? PendingTier { get; set; }

    public DateTime? TrialEndsAt { get; set; }

    public int TrialReminderDaySent { get; set; }

    public DateTime? CanceledAt { get; set; }
    public int FailedPaymentCount { get; set; }

    public string? StripeSubscriptionId { get; set; }
}
