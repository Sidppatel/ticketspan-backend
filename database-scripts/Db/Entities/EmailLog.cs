namespace Db.Entities;

public class EmailLog
{
    public Guid Id { get; set; }
    public Guid? TenantsId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string Recipient { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? Status { get; set; }
}
