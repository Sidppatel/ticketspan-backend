using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Protos.Enums;

namespace Svyne.Api.Services;

public sealed class EnumServiceImpl : EnumService.EnumServiceBase
{
    private readonly Db db;

    public EnumServiceImpl(Db db)
    {
        this.db = db;
    }

    public override async Task<ListEnumsResponse> ListEnums(ListEnumsRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListEnumsResponse();
        await using var connection = await db.OpenAsync(null, null, ct);
        var filter = string.IsNullOrEmpty(request.EnumType) ? string.Empty : " WHERE enum_type = @type";
        await using var cmd = new NpgsqlCommand(
            "SELECT enum_type, enum_value, int_value, used_in, description FROM enum_definitions"
            + filter + " ORDER BY enum_type, int_value", connection);
        if (!string.IsNullOrEmpty(request.EnumType))
        {
            cmd.Parameters.AddWithValue("type", request.EnumType);
        }
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Values.Add(new Svyne.Protos.Enums.EnumValue
            {
                EnumType = reader.GetString(0),
                Value = reader.GetString(1),
                IntValue = reader.GetInt32(2),
                UsedIn = reader.GetString(3),
                Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }
        return response;
    }
}
