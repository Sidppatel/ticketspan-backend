using System;

namespace Db.Entities.Views;

/// <summary>
/// Keyless projection over v_site_visits — exposes page view / visit logs
/// from the audit_logs table joined with user details.
/// </summary>
public class SiteVisitView
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public string? ScreenResolution { get; set; }
    public string? Portal { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BusinessUserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public string? UserRole { get; set; }
}
