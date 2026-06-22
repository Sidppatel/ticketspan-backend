using Db.Enums;

namespace Db.Entities;

public class Table : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Label { get; set; }
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public TableStatus Status { get; set; } = TableStatus.Available;
    public Guid? LockedByUsersId { get; set; }
    public User? LockedByUser { get; set; }
    public DateTime? LockExpiresAt { get; set; }

    public Guid EventTablesId { get; set; }
    public EventTable EventTable { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;
}
