using Db.Enums;

namespace Db.Entities;

public class PurchaseTicket : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string TicketCode { get; set; }
    public required string QrToken { get; set; }
    public int SeatNumber { get; set; }

    public Guid PurchasesId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public Guid? GuestUsersId { get; set; }
    public User? GuestUser { get; set; }

    public string? InviteTokenHash { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public string? InvitedEmail { get; set; }
    public DateTime? InviteSentAt { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Unassigned;
}
