using Grpc.Core;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.Security;
using Svyne.Protos.Admin;
using Svyne.Protos.Common;

namespace Svyne.Api.Services;

public sealed class FeedbackServiceImpl : FeedbackService.FeedbackServiceBase
{
    private readonly Db db;
    private readonly TenantContext tenantContext;

    public FeedbackServiceImpl(Db db, TenantContext tenantContext)
    {
        this.db = db;
        this.tenantContext = tenantContext;
    }

    public override async Task<UuidValue> CreateFeedback(CreateFeedbackRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_create_feedback(@name, @email, @type, @msg, @rating, @u, NULL, NULL, @diag::jsonb, @t)", connection);
        cmd.Parameters.AddWithValue("name", request.Name);
        cmd.Parameters.AddWithValue("email", (object?)NullIfEmpty(request.Email) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("type", request.Type);
        cmd.Parameters.AddWithValue("msg", request.Message);
        cmd.Parameters.AddWithValue("rating", request.Rating);
        cmd.Parameters.AddWithValue("u", (object?)tenantContext.UsersId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("diag", (object?)NullIfEmpty(request.DiagnosticsJson) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("t", (object?)tenantContext.TenantsId ?? DBNull.Value);
        var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        return new UuidValue { Value = id.ToString() };
    }

    public override async Task<ListFeedbackResponse> ListFeedback(PageRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var response = new ListFeedbackResponse { Meta = new PageMeta() };
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT feedback_id, name, type, message, rating, created_at FROM vw_feedbacks ORDER BY created_at DESC", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            response.Feedback.Add(new Feedback
            {
                FeedbacksId = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                Type = reader.GetString(2),
                Message = reader.GetString(3),
                Rating = reader.GetInt32(4),
                CreatedAt = new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero).ToUnixTimeSeconds()
            });
        }
        response.Meta.Total = response.Feedback.Count;
        return response;
    }

    public override async Task<AckResponse> DeleteFeedback(UuidValue request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_delete_feedback(@id)", connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(request.Value));
        await cmd.ExecuteNonQueryAsync(ct);
        return new AckResponse { Success = true, Message = "Feedback deleted" };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
