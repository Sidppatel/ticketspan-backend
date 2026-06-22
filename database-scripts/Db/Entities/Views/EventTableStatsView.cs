namespace Db.Entities.Views;

public class EventTableStatsView
{
    public Guid EventId { get; set; }
    public int TotalTables { get; set; }
    public int BookedTables { get; set; }
}
