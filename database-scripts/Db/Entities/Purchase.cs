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
    /// Acquisition channel captured at booking time: direct | social | email |
    /// qr | partner. Feeds the sales-by-channel report; never buyer-visible.
    /// </summary>
    public string SalesChannel { get; set; } = "direct";

    /// <summary>
    /// Hard expiry for an unpaid Pending hold. NULL once Paid/Cancelled/Expired.
    /// Seats/tables are reserved only while this is in the future.
    /// </summary>
    public DateTime? HoldExpiresAt { get; set; }

    /// <summary>Total seats across this booking's lines (denormalized rollup for
    /// analytics / display). Detail lives on <see cref="Lines"/>.</summary>
    public int? SeatsReserved { get; set; }

    public StripeTransaction? StripeTransaction { get; set; }

    /// <summary>Multi-item cart lines (each booking line represents a single seat/ticket/table).</summary>
    public ICollection<BookingLine> Lines { get; set; } = [];
}
