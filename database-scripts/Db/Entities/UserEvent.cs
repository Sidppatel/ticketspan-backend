namespace Db.Entities;

public class UserEvent : BaseEntity
{
    public Guid UsersId { get; set; }
    public User User { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid? AssignedByUsersId { get; set; }
    public User? AssignedByUser { get; set; }
}
