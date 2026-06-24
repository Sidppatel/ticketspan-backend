namespace Db.Entities;

public class BookingTable
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid BookingsId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid TablesId { get; set; }
    public Table Table { get; set; } = null!;
}
