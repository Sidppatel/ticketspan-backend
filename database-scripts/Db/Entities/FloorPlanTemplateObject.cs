using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A layout object (Entry/Exit/Stage) captured inside a <see cref="FloorPlanTemplate"/>.
/// </summary>
public class FloorPlanTemplateObject : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid FloorPlanTemplatesId { get; set; }
    public FloorPlanTemplate FloorPlanTemplate { get; set; } = null!;

    public LayoutObjectType ObjectType { get; set; } = LayoutObjectType.Stage;
    public string? Label { get; set; }
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}
