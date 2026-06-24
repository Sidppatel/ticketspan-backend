using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Booking;

namespace Svyne.Api.Services;

public sealed class CheckInServiceImpl : CheckInService.CheckInServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public CheckInServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<ScanResponse> Scan(ScanRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT success, message, guest_name, status_str FROM sp_check_in_ticket(@qr)", connection);
        cmd.Parameters.AddWithValue("qr", request.QrToken);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new ScanResponse { Valid = false, Message = "Ticket not found" };
        }
        return new ScanResponse
        {
            Valid = reader.GetBoolean(0),
            Message = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            HolderName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
        };
    }

    public override async Task<CheckInStats> GetCheckInStats(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT total, checked_in FROM sp_get_booking_stats(NULL, @ev)", connection);
        cmd.Parameters.AddWithValue("ev", Guid.Parse(request.Value));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new CheckInStats();
        }
        var total = reader.GetInt32(0);
        var checkedIn = reader.GetInt32(1);
        return new CheckInStats { Total = total, CheckedIn = checkedIn, Remaining = total - checkedIn };
    }
}
