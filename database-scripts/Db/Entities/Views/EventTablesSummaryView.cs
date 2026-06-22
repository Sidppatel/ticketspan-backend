namespace Db.Entities.Views;

public class EventTablesSummaryView
{
    public Guid EventTableId { get; set; }
    public Guid EventId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int DefaultRowSpan { get; set; }
    public int DefaultColSpan { get; set; }
    public bool IsActive { get; set; }

    // Aggregates
    public int TotalTables { get; set; }
    public int AvailableTables { get; set; }
    public int LockedTables { get; set; }
    public int BookedTables { get; set; }
}
