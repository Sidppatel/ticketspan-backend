namespace Db.Entities;

public class PlatformLead : BaseEntity
{
    public required string Name { get; set; }
    public required string CompanyName { get; set; }
    public required string Phone { get; set; }
    public string? Website { get; set; }
    public required string Description { get; set; }
}
