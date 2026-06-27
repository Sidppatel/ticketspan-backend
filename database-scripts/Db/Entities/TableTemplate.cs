using Db.Enums;

namespace Db.Entities;

public class TableTemplate : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Name { get; set; }
    public int DefaultCapacity { get; set; }
    public TableShape DefaultShape { get; set; } = TableShape.Round;
    public string? DefaultColor { get; set; }
    public int DefaultPriceCents { get; set; }
    public decimal DefaultWidth { get; set; } = 80;
    public decimal DefaultHeight { get; set; } = 80;
    public bool IsActive { get; set; } = true;

    public ICollection<EventTable> EventTables { get; set; } = [];
    public ICollection<TableTemplatePriceRule> PriceRules { get; set; } = [];
}
