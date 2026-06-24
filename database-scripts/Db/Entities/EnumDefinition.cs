namespace Db.Entities;

public class EnumDefinition : BaseEntity
{
    public required string EnumType { get; set; }
    public required string EnumValue { get; set; }
    public int IntValue { get; set; }
    public required string UsedIn { get; set; }
    public string? Description { get; set; }
}
