namespace Db.Entities;

/// <summary>
/// One line of a multi-item booking: a ticket tier (with seats) or a table
/// (seats = table capacity). A booking with lines aggregates their prices into
/// one transaction / one Stripe PaymentIntent. Single-line legacy bookings have
/// no lines and use the Booking's own table/ticket columns instead.
/// </summary>
public class BookingLine : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid BookingsId { get; set; }
    public Booking Booking { get; set; } = null!;

    /// <summary>"Ticket" | "Table".</summary>
    public required string Kind { get; set; }

    public Guid? EventTicketTypesId { get; set; }
    public EventTicketType? EventTicketType { get; set; }

    public Guid? TablesId { get; set; }
    public Table? Table { get; set; }

    /// <summary>Linked Pricing Module price snapshot used at reserve time.</summary>
    public Guid? PricesId { get; set; }
    public Price? Price { get; set; }

    /// <summary>Seats this line reserves. For a table = its capacity.</summary>
    public int Seats { get; set; }

    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }
}
