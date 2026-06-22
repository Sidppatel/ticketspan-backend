using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Db;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventPlatformDbContext>
{
    public EventPlatformDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=event_platform;Username=ep_dev;Password=ep_dev_password";

        if (connStr.StartsWith("postgres://") || connStr.StartsWith("postgresql://"))
        {
            var uri = new Uri(connStr);
            var userInfo = uri.UserInfo.Split(':');
            connStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]}";
        }

        var optionsBuilder = new DbContextOptionsBuilder<EventPlatformDbContext>();
        optionsBuilder.UseNpgsql(connStr).UseSnakeCaseNamingConvention();

        return new EventPlatformDbContext(optionsBuilder.Options);
    }
}
