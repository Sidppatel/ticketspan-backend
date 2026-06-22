namespace Db.Entities;

public class StripePayout : BaseEntity
{
    public required string StripePayoutId { get; set; }

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    public required string Status { get; set; }

    public DateTime? ArrivalDate { get; set; }
    public DateTime? PaidAt { get; set; }

    public string? RawEvent { get; set; }
}
