namespace Db.Entities.Views;

public class TicketView
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string QrToken { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Invite info
    public string? InvitedEmail { get; set; }
    public DateTime? InviteSentAt { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime? ClaimedAt { get; set; }

    // Booking
    public Guid BookingId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = string.Empty;

    // Guest user
    public Guid? GuestUserId { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestFirstName { get; set; }
    public string? GuestLastName { get; set; }

    // Event
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public DateTime EventEndDate { get; set; }

    // Venue
    public string VenueName { get; set; } = string.Empty;
    public string VenueCity { get; set; } = string.Empty;

    // Booking owner
    public Guid BookingUserId { get; set; }
    public string BookingUserEmail { get; set; } = string.Empty;
    public string BookingUserFirstName { get; set; } = string.Empty;
    public string BookingUserLastName { get; set; } = string.Empty;

    // Invite token hash (for claim-by-token lookups via view)
    public string? InviteTokenHash { get; set; }

    // Booking table (for claim info table label)
    public Guid? BookingTableId { get; set; }
}
