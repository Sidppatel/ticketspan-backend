namespace Db.Entities;

public class EventSponsor
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid SponsorsId { get; set; }
    public Sponsor Sponsor { get; set; } = null!;

    public int SortOrder { get; set; }
    public string EventMeta { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
