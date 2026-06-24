namespace Db.Entities.Views;

public class AdminDashboardStatsView
{
    public int TotalEvents { get; set; }
    public int PublishedEvents { get; set; }
    public int TotalBookings { get; set; }
    public int PaidBookings { get; set; }
    public int CheckedInBookings { get; set; }
    public long TotalRevenueCents { get; set; }
    public int TotalUsers { get; set; }
    public int TotalVenues { get; set; }
}
