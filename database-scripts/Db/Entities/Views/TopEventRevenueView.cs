namespace Db.Entities.Views;

public class TopEventRevenueView
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int BookingCount { get; set; }
    public long RevenueCents { get; set; }
}
