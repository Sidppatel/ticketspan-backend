namespace Db.Entities;

public class Performer : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? PrimaryImagePath { get; set; }
    public string Meta { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
}
