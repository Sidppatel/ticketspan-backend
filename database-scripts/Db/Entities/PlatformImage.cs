namespace Db.Entities;

public class PlatformImage : BaseEntity
{
    public required Guid ImagesId { get; set; }
    public string? Tag { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    public Image Image { get; set; } = null!;
}
