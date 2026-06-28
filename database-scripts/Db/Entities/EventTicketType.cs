namespace Db.Entities;

public class EventTicketType : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Label { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public Guid? FeeFormulasId { get; set; }
    public FeeFormula? FeeFormula { get; set; }
    public int? MaxQuantity { get; set; }

    /// <summary>Seats this tier contributes to the event's calculated capacity. Null = uncapped.</summary>
    public int? Capacity { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    /// <summary>Link to the Pricing Module price (authoritative). Null during migration of legacy rows.</summary>
    public Guid? PricesId { get; set; }
    public Price? Price { get; set; }
}
