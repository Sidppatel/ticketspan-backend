using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A table placement captured inside a <see cref="FloorPlanTemplate"/>. Stores
/// position, span and table-type attributes (snapshot, by label) so the template
/// can recreate event_tables/tables when applied.
/// </summary>
public class FloorPlanTemplateTable : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid FloorPlanTemplatesId { get; set; }
    public FloorPlanTemplate FloorPlanTemplate { get; set; } = null!;

    public required string Label { get; set; }
    public required string TypeLabel { get; set; }
    public int Capacity { get; set; }
    public TableShape Shape { get; set; } = TableShape.Round;
    public string? Color { get; set; }
    public int PriceCents { get; set; }

    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public int SortOrder { get; set; }
}
