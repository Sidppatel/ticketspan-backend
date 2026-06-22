namespace Db.Entities.Views;

public class EventFacetsView
{
    public Guid EventId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EndDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueCity { get; set; } = string.Empty;
    public int? PricePerPersonCents { get; set; }
}
