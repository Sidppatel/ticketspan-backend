using System;

namespace Db.Entities;

public class StaffEventAccess : BaseEntity
{
    public Guid StaffUserId { get; set; }
    public User StaffUser { get; set; } = null!;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? AssignedByAdminId { get; set; }
    public User? AssignedByAdmin { get; set; }

    public DateTime? AccessStart { get; set; }
    public DateTime? AccessEnd { get; set; }
}
