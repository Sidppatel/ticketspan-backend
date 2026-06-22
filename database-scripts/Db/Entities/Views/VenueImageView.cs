namespace Db.Entities.Views;

public class VenueImageView
{
    public Guid VenueImageId { get; set; }
    public Guid VenueId { get; set; }
    public Guid ImageId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? OriginalName { get; set; }
    public int SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? ContentType { get; set; }
    public string? AltText { get; set; }
    public string? Caption { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
