namespace Db.Entities.Views;

public class TableView
{
    public Guid TableId { get; set; }
    public Guid EventId { get; set; }
    public Guid EventTableId { get; set; }
    public string Label { get; set; } = null!;
    public int GridRow { get; set; }
    public int GridCol { get; set; }
    public int RowSpan { get; set; }
    public int ColSpan { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = null!;
    public Guid? LockedByUserId { get; set; }
    public DateTime? LockExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Joined from EventTable
    public int Capacity { get; set; }
    public string Shape { get; set; } = null!;
    public string? Color { get; set; }
    public int PriceCents { get; set; }
    public int? PlatformFeeCents { get; set; }
    public int TotalPriceCents { get; set; }
    public string EventTableLabel { get; set; } = null!;
}
