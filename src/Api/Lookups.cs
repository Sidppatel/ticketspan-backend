namespace TicketSpan.Api;

public static class Lookups
{
    public static class UserRoles
    {
        public const int PublicViewer = 0;
        public const int Admin = 1;
        public const int Staff = 2;
        public const int SubTenant = 3;
        public const int EventManager = 4;
        public const int Developer = 99;
    }
}
