using Db.Enums;

namespace Db.Entities.Views;

public class StripeTransactionView
{
    public Guid TransactionId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundId { get; set; }
    public int? TransferAmountCents { get; set; }
    public int? StripeFeesCents { get; set; }
    public int? TotalChargedCents { get; set; }
    public DateTime CreatedAt { get; set; }

    // Purchase info
    public Guid PurchaseId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public PurchaseStatus PurchaseStatus { get; set; }

    // Event info
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    // User info
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;
    public string UserLastName { get; set; } = string.Empty;

    // Multi-tenant scoping
    public Guid OrganizationId { get; set; }
}
