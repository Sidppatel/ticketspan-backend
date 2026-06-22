namespace Db.Entities.Views;

public class PurchaseView
{
    public Guid PurchaseId { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }
    public string? QrToken { get; set; }
    public int? SeatsReserved { get; set; }
    public DateTime CreatedAt { get; set; }

    // User
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserFirstName { get; set; } = string.Empty;
    public string UserLastName { get; set; } = string.Empty;

    // Event
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventSlug { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public string? EventCategory { get; set; }
    public string? EventImagePath { get; set; }

    // Venue
    public string VenueName { get; set; } = string.Empty;
    public string VenueAddress { get; set; } = string.Empty;
    public string VenueCity { get; set; } = string.Empty;
    public string VenueState { get; set; } = string.Empty;

    // Table
    public Guid? TableId { get; set; }
    public string? TableLabel { get; set; }
    public string[] TableLabels { get; set; } = [];

    // Ticket type (Open events)
    public Guid? EventTicketTypeId { get; set; }
    public string? EventTicketTypeLabel { get; set; }

    // Stripe transaction
    public Guid? StripeTransactionId { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? TaxCalculationId { get; set; }
    public string? TaxTransactionId { get; set; }
    public string? PaymentStatus { get; set; }
    public int? PaymentAmountCents { get; set; }
    public int? TotalChargedCents { get; set; }
    public int? TaxAmountCents { get; set; }
    public int? StripeFeesCents { get; set; }
    public int? TransferAmountCents { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    // Aggregates
    public int TicketCount { get; set; }

    // Organizer
    public Guid BusinessUserId { get; set; }
}
