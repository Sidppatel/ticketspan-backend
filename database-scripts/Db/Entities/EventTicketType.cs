namespace Db.Entities;

public class EventTicketType : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Label { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int? MaxQuantity { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;
}
