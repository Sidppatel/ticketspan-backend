namespace Db.Entities.Views;

public class EventView
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string Category { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public string LayoutMode { get; set; } = null!;
    public int? MaxCapacity { get; set; }
    public int? PricePerPersonCents { get; set; }
    public int? GridRows { get; set; }
    public int? GridCols { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? ScheduledPublishAt { get; set; }
    public Guid VenueId { get; set; }
    public Guid BusinessUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string VenueName { get; set; } = null!;
    public string VenueAddress { get; set; } = null!;
    public string VenueCity { get; set; } = null!;
    public string VenueState { get; set; } = null!;
    public string VenueZipCode { get; set; } = null!;
    public string? VenueDescription { get; set; }
    public string? VenueImagePath { get; set; }
    public string? VenuePhone { get; set; }
    public string? VenueEmail { get; set; }
    public string? VenueWebsite { get; set; }
    public bool VenueIsActive { get; set; }
    public DateTime VenueCreatedAt { get; set; }

    public string OrganizerFirstName { get; set; } = null!;
    public string OrganizerLastName { get; set; } = null!;

    public int TotalCapacity { get; set; }
    public int TotalSold { get; set; }
    public int AvailableTables { get; set; }
    public int? MinTablePriceCents { get; set; }
    public int? MinTicketTypePriceCents { get; set; }
    public int? DisplayMinTablePriceCents { get; set; }
    public int? DisplayMinTicketTypePriceCents { get; set; }

    public string Performers { get; set; } = "[]";
    public string Sponsors { get; set; } = "[]";
}
