using Db.Enums;

namespace Db.Entities;

public class BookingLine : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid BookingsId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid? EventsId { get; set; }
    public Event? Event { get; set; }

    public required string Kind { get; set; }

    public Guid? EventTicketTypesId { get; set; }
    public EventTicketType? EventTicketType { get; set; }

    public Guid? TablesId { get; set; }
    public Table? Table { get; set; }

    public Guid? PricesId { get; set; }
    public Price? Price { get; set; }

    public int Seats { get; set; } = 1;

    public string? TicketCode { get; set; }
    public string? QrToken { get; set; }
    public int? SeatNumber { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Unassigned;

    public Guid? GuestUsersId { get; set; }
    public User? GuestUser { get; set; }

    public string? InviteTokenHash { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public string? InvitedEmail { get; set; }
    public DateTime? InviteSentAt { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public int BasePriceCents { get; set; }

    public int SellingPriceCents { get; set; }

    public int DiscountCents { get; set; }

    public Guid? AppliedPriceRulesId { get; set; }

    public string? AppliedRuleName { get; set; }

    public int PlatformFeeCents { get; set; }
    public int GatewayFeeCents { get; set; }

    public int FinalPriceCents { get; set; }

    public string Currency { get; set; } = "usd";

    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }
}
