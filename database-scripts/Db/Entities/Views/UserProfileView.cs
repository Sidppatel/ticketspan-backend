namespace Db.Entities.Views;

public class UserProfileView
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Phone { get; set; }
    public bool OptInLocationEmail { get; set; }
    public bool HasCompletedOnboarding { get; set; }
    public string? ImageStorageKey { get; set; }
    public DateTime CreatedAt { get; set; }

    // Address
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}
