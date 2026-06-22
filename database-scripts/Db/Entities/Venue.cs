namespace Db.Entities;

public class Venue : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? AddressesId { get; set; }
    public Address? Address { get; set; }

    public ICollection<Event> Events { get; set; } = [];
}
