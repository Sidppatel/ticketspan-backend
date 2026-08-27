namespace Db.Entities;

public class FloorPlanTemplate : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<FloorPlanTemplateTable> Tables { get; set; } = [];
    public ICollection<FloorPlanTemplateObject> Objects { get; set; } = [];
}
