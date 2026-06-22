namespace Db.Entities;

public class Feedback : BaseEntity
{
    public Guid? TenantsId { get; set; }
    public Tenant? Tenant { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public required string Type { get; set; }
    public required string Message { get; set; }
    public int Rating { get; set; }
    public Guid? UsersId { get; set; }
    public User? User { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? Diagnostics { get; set; }
}
