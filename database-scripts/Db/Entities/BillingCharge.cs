namespace Db.Entities;

/// <summary>
/// Immutable ledger of every platform charge outside per-ticket fees:
/// subscription renewals, prorations, Pay Per Event purchases, add-on charges,
/// setup fees, and refunds (negative amounts). Source of truth for developer
/// revenue reports. Never deleted (7-year financial retention).
/// </summary>
public class BillingCharge : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>subscription | proration | pay_per_event | addon | setup_fee | refund.</summary>
    public required string Kind { get; set; }

    /// <summary>Tier or add-on type this charge relates to (for by-tier reports).</summary>
    public string? Reference { get; set; }

    public Guid? EventsId { get; set; }

    /// <summary>Negative for refunds/credits.</summary>
    public int AmountCents { get; set; }

    public required string Description { get; set; }

    public string? StripePaymentIntentId { get; set; }
}
