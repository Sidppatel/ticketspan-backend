namespace Db.Entities;

public class EventImage : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required Guid EventsId { get; set; }
    public required Guid ImagesId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Event Event { get; set; } = null!;
    public Image Image { get; set; } = null!;
}
