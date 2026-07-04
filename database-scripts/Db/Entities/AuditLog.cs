using Db.Enums;

namespace Db.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantsId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public required string EventType { get; set; }
    public required AuditActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? EventsId { get; set; }
    public required string Action { get; set; }
    public string? MetadataJson { get; set; }
    public string? Ip { get; set; }
    public Guid? CorrelationId { get; set; }
}
