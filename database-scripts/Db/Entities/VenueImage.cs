namespace Db.Entities;

public class VenueImage : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required Guid VenuesId { get; set; }
    public required Guid ImagesId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Venue Venue { get; set; } = null!;
    public Image Image { get; set; } = null!;
}
