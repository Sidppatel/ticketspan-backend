namespace Db.Entities;

public class ScheduleItem : BaseEntity
{
    public required string Title { get; set; }
    public required string TypeCategory { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
