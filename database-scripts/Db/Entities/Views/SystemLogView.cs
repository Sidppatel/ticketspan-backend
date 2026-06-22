namespace Db.Entities.Views;

public class SystemLogView
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public string? CorrelationId { get; set; }
    public long? DurationMs { get; set; }
    public string? MetadataJson { get; set; }
}
