namespace Db.Entities;

public class EventSponsorProjection
{
    public Guid EventId { get; set; }
    public Guid SponsorId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? PrimaryImagePath { get; set; }
    public int SortOrder { get; set; }
    public List<SponsorMetaItem> EffectiveMeta { get; set; } = new();
}
