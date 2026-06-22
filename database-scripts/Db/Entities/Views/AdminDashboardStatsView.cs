namespace Db.Entities.Views;

public class AdminDashboardStatsView
{
    public int TotalEvents { get; set; }
    public int PublishedEvents { get; set; }
    public int TotalPurchases { get; set; }
    public int PaidPurchases { get; set; }
    public int CheckedInPurchases { get; set; }
    public long TotalRevenueCents { get; set; }
    public int TotalUsers { get; set; }
    public int TotalVenues { get; set; }
}
