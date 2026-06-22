namespace Db.Entities.Views;

public class EventTicketTypeSummaryView
{
    public Guid EventTicketTypeId { get; set; }
    public Guid EventId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int? MaxQuantity { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    // Computed
    public int TotalPriceCents { get; set; }

    // Aggregates
    public int SoldCount { get; set; }
    public int AvailableCount { get; set; }
}
