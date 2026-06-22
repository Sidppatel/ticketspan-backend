namespace Db.Enums;

/// <summary>
/// Canonical audit-log event type strings. String-backed (not a C# enum) to allow
/// free-form values from legacy call sites while keeping constants for common flows.
/// </summary>
public static class AuditEventType
{
    // Admin / event management
    public const string EventCreated = "event.created";
    public const string EventUpdated = "event.updated";
    public const string EventDeleted = "event.deleted";
    public const string EventDuplicated = "event.duplicated";
    public const string EventImageAdd = "event.image.add";
    public const string EventImageReorder = "event.image.reorder";
    public const string EventImageSetPrimary = "event.image.setPrimary";
    public const string EventImageDelete = "event.image.delete";
    public const string EventTicketTypeCreated = "event.ticket_type.created";
    public const string StaffAssignedToEvent = "staff.assigned_to_event";
    public const string StaffUnassignedFromEvent = "staff.unassigned_from_event";

    // Images
    public const string ImageUploaded = "image.uploaded";
    public const string ImageDeleted = "image.deleted";

    // Auth / user lifecycle
    public const string UserLoginSuccess = "auth.login.success";
    public const string UserLoginFailed = "auth.login.failed";
    public const string UserLogout = "auth.logout";

    // Developer / system
    public const string BackgroundJob = "system.background_job";
    public const string MigrationApplied = "system.migration_applied";
    public const string UnhandledException = "developer.unhandled_exception";
}
