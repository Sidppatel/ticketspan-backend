namespace Db.Entities;

/// <summary>
/// Pay Per Event purchase: unlocks lower fees + features for one event,
/// independent of the tenant's tier. The per-ticket fee is applied by pointing
/// events.fee_formulas_id at the tier's formula on activation. Developer-managed.
/// </summary>
public class EventUpgrade : BaseEntity
{
    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>starter_event | pro_event | business_event | enterprise_event.</summary>
    public required string Tier { get; set; }

    /// <summary>active | canceled | refunded.</summary>
    public string Status { get; set; } = "active";

    public int PriceCents { get; set; }
    public int SmsCredits { get; set; }
    public int CustomDomainLimit { get; set; }

    public DateTime? CanceledAt { get; set; }
    public int RefundedCents { get; set; }

    public string? StripePaymentIntentId { get; set; }
}
