namespace Db.Entities;

/// <summary>
/// A reusable, tenant-scoped snapshot of a whole floor plan (grid + tables +
/// objects). Distinct from <see cref="TableTemplate"/>, which is a single table
/// type. Applied to a new event via sp_apply_floor_plan_template.
/// </summary>
public class FloorPlanTemplate : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public required string Name { get; set; }
    public int GridRows { get; set; }
    public int GridCols { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FloorPlanTemplateTable> Tables { get; set; } = [];
    public ICollection<FloorPlanTemplateObject> Objects { get; set; } = [];
}
