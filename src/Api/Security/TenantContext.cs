namespace Svyne.Api.Security;

public sealed class TenantContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public Guid? UsersId { get; set; }
    public Guid? TenantsId { get; set; }
    public int Role { get; set; }
    public string TenantSlug { get; set; } = string.Empty;

    public bool IsDeveloper => Role == 99;

    public bool IsEventScoped => Role == 2 || Role == 4;
}
