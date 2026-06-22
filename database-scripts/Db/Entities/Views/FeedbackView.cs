namespace Db.Entities.Views;

public class FeedbackView
{
    public Guid FeedbackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }
    public Guid? UserId { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? Diagnostics { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserFullName { get; set; }
}
