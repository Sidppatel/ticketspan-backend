using Db.Enums;

namespace Db.Entities;

public class StripeTransaction : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid BookingsId { get; set; }
    public Booking Booking { get; set; } = null!;

    public required string PaymentIntentId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.RequiresConfirmation;
    public string Currency { get; set; } = "usd";

    public int AmountCents { get; set; }
    public int? TransferAmountCents { get; set; }

    public string? TaxCalculationId { get; set; }
    public string? TaxTransactionId { get; set; }

    public int? TotalChargedCents { get; set; }
    public int? TaxAmountCents { get; set; }
    public int? StripeFeesCents { get; set; }

    public DateTime? PaidAt { get; set; }
    public string? RefundId { get; set; }
    public DateTime? RefundedAt { get; set; }
}
