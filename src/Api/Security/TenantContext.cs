namespace TicketSpan.Api.Security;

public sealed class TenantContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public Guid? UsersId { get; set; }
    public Guid? TenantsId { get; set; }
    public int Role { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public bool IsActingForTenant { get; set; }
    public bool NotifyTenant { get; set; }

    public bool IsDeveloper => Role == Lookups.UserRoles.Developer;

    public bool IsEventScoped => Role == Lookups.UserRoles.Staff || Role == Lookups.UserRoles.EventManager;
}
