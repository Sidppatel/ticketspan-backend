namespace Db.Entities;

public class EventPerformerProjection
{
    public Guid EventId { get; set; }
    public Guid PerformerId { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? PrimaryImagePath { get; set; }
    public int SortOrder { get; set; }
    public List<PerformerMetaItem> EffectiveMeta { get; set; } = new();
}
