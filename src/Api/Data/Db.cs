using Npgsql;

namespace Svyne.Api.Data;

public sealed class Db
{
    private readonly string connectionString;

    public Db(IConfiguration configuration)
    {
        var raw = configuration["DATABASE_URL"]
            ?? "Host=localhost;Port=5432;Database=event_platform;Username=ep_dev;Password=ep_dev_password";
        if (raw.StartsWith("postgres://") || raw.StartsWith("postgresql://"))
        {
            var uri = new Uri(raw);
            var userInfo = uri.UserInfo.Split(':');
            raw = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]}";
        }
        connectionString = raw;
    }

    public async Task<NpgsqlConnection> OpenAsync(Guid? usersId, Guid? tenantsId, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        if (usersId is { } u)
        {
            await SetConfigAsync(connection, "app.current_user_id", u.ToString(), ct);
        }
        if (tenantsId is { } t)
        {
            await SetConfigAsync(connection, "app.current_tenant", t.ToString(), ct);
        }
        return connection;
    }

    private static async Task SetConfigAsync(NpgsqlConnection connection, string key, string value, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config(@k, @v, false)", connection);
        cmd.Parameters.AddWithValue("k", key);
        cmd.Parameters.AddWithValue("v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
