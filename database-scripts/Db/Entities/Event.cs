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

    /// <summary>
    /// How the event sells: <see cref="EventType.Open"/> (ticket tiers only),
    /// <see cref="EventType.Table"/> (floor plan only), or <see cref="EventType.Both"/>
    /// (optional open capacity + floor plan). Gates which sellables can be created
    /// and which capacity rules apply at checkout.
    /// </summary>
    public EventType EventType { get; set; } = EventType.Open;

    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }

    /// <summary>
    /// When true, buyers see a single all-in total (developer fee folded into the
    /// shown price). When false, the fee is itemized: price + fee = total. Display
    /// only — the math is identical; admins cannot change the fee amount itself.
    /// </summary>
    public bool FeesIncluded { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }

    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid VenuesId { get; set; }
    public Venue Venue { get; set; } = null!;

    public Guid CreatedByUsersId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
