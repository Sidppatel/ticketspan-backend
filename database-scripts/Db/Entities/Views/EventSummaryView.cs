namespace Db.Entities.Views;

public class EventSummaryView
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Category { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ImagePath { get; set; }
    public string? PrimaryImageKey { get; set; }
    public bool IsFeatured { get; set; }
    public string LayoutMode { get; set; } = null!;
    public int? PricePerPersonCents { get; set; }
    public int? MaxCapacity { get; set; }
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = null!;
    public string VenueCity { get; set; } = null!;
    public string VenueState { get; set; } = null!;
    public Guid BusinessUserId { get; set; }
    public string OrganizerName { get; set; } = null!;
    public int TotalCapacity { get; set; }
    public int TotalSold { get; set; }
    public int AvailableTables { get; set; }
    public int? MinTablePriceCents { get; set; }
    public int? MinTicketTypePriceCents { get; set; }
    public int? DisplayMinTablePriceCents { get; set; }
    public int? DisplayMinTicketTypePriceCents { get; set; }
    public DateTime CreatedAt { get; set; }
}
