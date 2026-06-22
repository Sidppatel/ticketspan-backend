namespace Db.Entities;

public class PurchaseTable
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PurchasesId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public Guid TablesId { get; set; }
    public Table Table { get; set; } = null!;
}
