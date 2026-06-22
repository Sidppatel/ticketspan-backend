namespace Db.Entities.Views;

public class SponsorView
{
    public Guid SponsorId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? PrimaryImagePath { get; set; }
    public string Meta { get; set; } = "[]";
    public int EventCount { get; set; }
    public int UpcomingEventCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
