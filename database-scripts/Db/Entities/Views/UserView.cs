namespace Db.Entities.Views;

public class UserView
{
    public Guid UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string EmailHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public short Role { get; set; }
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ImageStorageKey { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
