namespace Db.Entities;

public class PerformerMetaItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
}
