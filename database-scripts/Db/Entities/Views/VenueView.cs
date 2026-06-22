namespace Db.Entities.Views;

public class VenueView
{
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Address
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    // Aggregates
    public int EventCount { get; set; }
    public string? PrimaryImageKey { get; set; }
}
