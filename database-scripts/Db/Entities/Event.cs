using NpgsqlTypes;
using Db.Enums;

namespace Db.Entities;

public class Event : BaseEntity
{
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public EventCategory? Category { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public LayoutMode LayoutMode { get; set; }
    public int? MaxCapacity { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public int? GridRows { get; set; }
    public int? GridCols { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid VenuesId { get; set; }
    public Venue Venue { get; set; } = null!;

    public Guid CreatedByUsersId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
