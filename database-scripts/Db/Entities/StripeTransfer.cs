namespace Db.Entities;

public class StripeTransfer : BaseEntity
{
    public required string StripeTransferId { get; set; }

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid? BookingsId { get; set; }
    public Booking? Booking { get; set; }

    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";

    public string? RawEvent { get; set; }
}
