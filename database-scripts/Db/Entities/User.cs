namespace Db.Entities;

public class User : BaseEntity
{
    public Guid? TenantsId { get; set; }
    public Tenant? Tenant { get; set; }

    public required string Email { get; set; }
    public required string EmailHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    public string? PasswordHash { get; set; }
    public short PepperVersion { get; set; } = 1;

    public string? GoogleSubject { get; set; }

    public short Role { get; set; }

    public string? Phone { get; set; }

    public Guid? ImagesId { get; set; }
    public Image? Image { get; set; }

    public Guid? AddressesId { get; set; }
    public Address? Address { get; set; }

    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastRequestAt { get; set; }

    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }

    public bool OptInLocationEmail { get; set; }
    public bool HasCompletedOnboarding { get; set; }
}
