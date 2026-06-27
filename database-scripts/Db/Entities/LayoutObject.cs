using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A non-table object placed on an event's floor-plan grid (Entry, Exit, Stage).
/// Movable like tables but not sellable.
/// </summary>
public class LayoutObject : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public LayoutObjectType ObjectType { get; set; } = LayoutObjectType.Stage;
    public string? Label { get; set; }
    public decimal PosX { get; set; }
    public decimal PosY { get; set; }
    public decimal Width { get; set; } = 80;
    public decimal Height { get; set; } = 80;
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}
