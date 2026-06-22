namespace Db.Entities;

public class AppSetting : BaseEntity
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
}
