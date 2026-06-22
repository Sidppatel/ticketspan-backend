using Db.Enums;

namespace Db.Entities;

public class Purchase : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string PurchaseNumber { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;

    public Guid UsersId { get; set; }
    public User User { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public int SubtotalCents { get; set; }
    public int FeeCents { get; set; }
    public int TotalCents { get; set; }

    public string? QrToken { get; set; }

    public Guid? TablesId { get; set; }
    public Table? Table { get; set; }

    public int? SeatsReserved { get; set; }

    public Guid? EventTicketTypesId { get; set; }
    public EventTicketType? EventTicketType { get; set; }

    public StripeTransaction? StripeTransaction { get; set; }

    public ICollection<PurchaseTicket> Tickets { get; set; } = [];
}
