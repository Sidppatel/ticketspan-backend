namespace Db.Entities.Views;

public class BusinessLogView
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? BusinessUserId { get; set; }
    public string? BusinessUserEmail { get; set; }
    public string? BusinessUserRole { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
}
