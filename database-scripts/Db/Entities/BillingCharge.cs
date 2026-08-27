namespace Db.Entities;

public class BillingCharge : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public required string Kind { get; set; }

    public string? Reference { get; set; }

    public Guid? EventsId { get; set; }

    public int AmountCents { get; set; }

    public required string Description { get; set; }

    public string? StripePaymentIntentId { get; set; }
}
