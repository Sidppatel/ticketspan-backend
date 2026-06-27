using Db.Enums;

namespace Db.Entities;

public class Booking : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string BookingNumber { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public Guid UsersId { get; set; }
    public User User { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }

    public string? QrToken { get; set; }

    /// <summary>
    /// Hard expiry for an unpaid Pending hold. NULL once Paid/Cancelled/Expired.
    /// Seats/tables are reserved only while this is in the future.
    /// </summary>
    public DateTime? HoldExpiresAt { get; set; }

    public Guid? TablesId { get; set; }
    public Table? Table { get; set; }

    public int? SeatsReserved { get; set; }

    public Guid? EventTicketTypesId { get; set; }
    public EventTicketType? EventTicketType { get; set; }

    public StripeTransaction? StripeTransaction { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = [];

    /// <summary>Multi-item cart lines. Empty for single-line legacy bookings.</summary>
    public ICollection<BookingLine> Lines { get; set; } = [];
}
