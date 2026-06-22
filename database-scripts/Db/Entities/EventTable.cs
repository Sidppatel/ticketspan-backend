using Db.Enums;

namespace Db.Entities;

public class EventTable : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Label { get; set; }
    public int Capacity { get; set; }
    public TableShape Shape { get; set; } = TableShape.Round;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int? RowSpan { get; set; }
    public int? ColSpan { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? TableTemplatesId { get; set; }
    public TableTemplate? TableTemplate { get; set; }

    public ICollection<Table> Tables { get; set; } = [];
}
