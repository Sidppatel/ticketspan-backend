namespace Db.Entities.Views;

public class TopEventRevenueView
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public long RevenueCents { get; set; }
}
