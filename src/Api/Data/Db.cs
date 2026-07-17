using Npgsql;

namespace TicketSpan.Api.Data;

public sealed class Db
{
    private readonly string connectionString;
    private readonly string bootstrapConnectionString;

    public Db(IConfiguration configuration)
    {
        var host = configuration["DB_HOST"] ?? "127.0.0.1";
        var port = configuration["DB_PORT"] ?? "5432";
        var user = configuration["DB_USER"] ?? "ep_dev";
        var database = configuration["DB_NAME"] ?? "event_platform";
        var password = configuration["DB_PASSWORD"] ?? "ep_dev_password";
        var sslMode = configuration["DATABASE_SSL_MODE"] ?? "Disable";

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Username = user,
            Database = database,
            Password = password,
            SslMode = Enum.Parse<SslMode>(sslMode, ignoreCase: true)
        };
        connectionString = builder.ConnectionString;

        
        builder.Username = configuration["DB_BOOTSTRAP_USER"] ?? user;
        builder.Password = configuration["DB_BOOTSTRAP_PASSWORD"] ?? password;
        bootstrapConnectionString = builder.ConnectionString;
    }

    
    public async Task<NpgsqlConnection> OpenBootstrapAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(bootstrapConnectionString);
        await connection.OpenAsync(ct);
        return connection;
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
