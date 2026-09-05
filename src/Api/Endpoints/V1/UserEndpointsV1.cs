using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.Endpoints.V1;

public static class UserEndpointsV1
{
    public record UpdateProfileApiRequest(
        string? FirstName,
        string? LastName,
        string? Phone,
        string? Address,
        string? City,
        string? State,
        string? Zip,
        bool? EmailOptIn,
        string? Bio,
        string? Pronouns,
        string? PreferencesJson,
        string? BillingAddress,
        string? BillingCity,
        string? BillingState,
        string? BillingZip
    );

    public record UserProfileApiResponse(
        string UsersId,
        string TenantsId,
        int Role,
        string TenantSlug,
        string Email,
        string FirstName,
        string LastName,
        bool EmailVerified,
        string Phone,
        string AvatarUrl,
        string Address,
        string City,
        string State,
        string Zip,
        bool GoogleConnected,
        string Bio,
        string Pronouns,
        string PreferencesJson,
        string BillingAddress,
        string BillingCity,
        string BillingState,
        string BillingZip
    );

    public static RouteGroupBuilder MapUserApiV1(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization();

        users.MapGet("/me", async (
            HttpContext httpContext,
            TenantContext tc,
            Db db,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (tc.UsersId is not { } usersId)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT email, first_name, last_name, email_verified, COALESCE(phone, ''), images_id, "
                + "COALESCE(address_line1, ''), COALESCE(city, ''), COALESCE(state, ''), COALESCE(zip_code, ''), "
                + "google_connected, "
                + "COALESCE(bio, ''), COALESCE(pronouns, ''), COALESCE(preferences_json, ''), "
                + "COALESCE(billing_address_line, ''), COALESCE(billing_city, ''), COALESCE(billing_state, ''), COALESCE(billing_zip, '') "
                + "FROM vw_user_profile WHERE users_id = @id", connection);
            cmd.Parameters.AddWithValue("id", usersId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound();
            }

            var imagesId = reader.IsDBNull(5) ? (Guid?)null : reader.GetGuid(5);
            var baseUrl = configuration["PUBLIC_BASE_URL"] ?? string.Empty;

            var profile = new UserProfileApiResponse(
                usersId.ToString(),
                tc.TenantsId?.ToString() ?? string.Empty,
                tc.Role,
                tc.TenantSlug,
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetString(4),
                imagesId is { } img ? $"{baseUrl}/images/{img}" : string.Empty,
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetBoolean(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetString(14),
                reader.GetString(15),
                reader.GetString(16),
                reader.GetString(17)
            );

            return Results.Ok(profile);
        });

        users.MapPut("/me", async (
            UpdateProfileApiRequest request,
            HttpContext httpContext,
            TenantContext tc,
            Db db,
            CancellationToken ct) =>
        {
            if (tc.UsersId is not { } usersId)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT sp_update_user_profile(@uid, @first, @last, @phone, @addr, @city, @state, @zip, @opt, @bio, @pronouns, @prefs::jsonb, @baddr, @bcity, @bstate, @bzip)",
                connection);
            cmd.Parameters.AddWithValue("uid", usersId);
            cmd.Parameters.AddWithValue("first", request.FirstName ?? string.Empty);
            cmd.Parameters.AddWithValue("last", request.LastName ?? string.Empty);
            cmd.Parameters.AddWithValue("phone", request.Phone ?? string.Empty);
            cmd.Parameters.AddWithValue("addr", request.Address ?? string.Empty);
            cmd.Parameters.AddWithValue("city", request.City ?? string.Empty);
            cmd.Parameters.AddWithValue("state", request.State ?? string.Empty);
            cmd.Parameters.AddWithValue("zip", request.Zip ?? string.Empty);
            cmd.Parameters.AddWithValue("opt", (object?)request.EmailOptIn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bio", (object?)request.Bio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pronouns", (object?)request.Pronouns ?? DBNull.Value);
            cmd.Parameters.AddWithValue("prefs", string.IsNullOrWhiteSpace(request.PreferencesJson) ? DBNull.Value : request.PreferencesJson);
            cmd.Parameters.AddWithValue("baddr", (object?)request.BillingAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bcity", (object?)request.BillingCity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bstate", (object?)request.BillingState ?? DBNull.Value);
            cmd.Parameters.AddWithValue("bzip", (object?)request.BillingZip ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok(new { Success = true });
        });

        users.MapPost("/me/avatar", async (
            HttpContext httpContext,
            TenantContext tc,
            Db db,
            HttpRequest req,
            CancellationToken ct) =>
        {
            if (tc.UsersId is not { } usersId)
            {
                return Results.Unauthorized();
            }

            var body = await req.ReadFromJsonAsync<AvatarApiRequest>(cancellationToken: ct);
            if (body is null || !Guid.TryParse(body.ImagesId, out var imgId))
            {
                return Results.BadRequest("Valid imagesId is required");
            }

            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand("SELECT sp_set_user_image(@uid, @img)", connection);
            cmd.Parameters.AddWithValue("uid", usersId);
            cmd.Parameters.AddWithValue("img", imgId);
            await cmd.ExecuteNonQueryAsync(ct);

            return Results.Ok(new { Success = true });
        });

        users.MapDelete("/me/avatar", async (
            HttpContext httpContext,
            TenantContext tc,
            Db db,
            CancellationToken ct) =>
        {
            if (tc.UsersId is not { } usersId)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(usersId, tc.TenantsId, ct);
            await using var cmd = new NpgsqlCommand("SELECT sp_set_user_image(@uid, NULL)", connection);
            cmd.Parameters.AddWithValue("uid", usersId);
            await cmd.ExecuteNonQueryAsync(ct);

            return Results.Ok(new { Success = true });
        });

        group.MapPost("/users/register", async (
            RegisterUserApiRequest request,
            Db db,
            PasswordHasher passwordHasher,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest("Email and password are required.");
            }

            var emailHash = EmailHasher.Hash(request.Email);
            var pwdHash = await passwordHasher.HashAsync(request.Password);

            await using var connection = await db.OpenAsync(null, null, ct);
            await using var cmd = new NpgsqlCommand(
                "SELECT users_id, email, first_name, last_name, role FROM sp_signup_attendee(NULL, @email, @hash, @first, @last, @pwd)",
                connection);
            cmd.Parameters.AddWithValue("email", request.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("hash", emailHash);
            cmd.Parameters.AddWithValue("first", request.FirstName?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("last", request.LastName?.Trim() ?? string.Empty);
            cmd.Parameters.AddWithValue("pwd", pwdHash);

            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    return Results.Ok(new
                    {
                        Success = true,
                        UsersId = reader.GetGuid(0),
                        Email = reader.GetString(1)
                    });
                }
                return Results.BadRequest("Failed to register user.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Results.Conflict("An account with this email already exists.");
            }
        }).AllowAnonymous();

        return group;
    }

    public record AvatarApiRequest(string ImagesId);
    public record RegisterUserApiRequest(string Email, string Password, string? FirstName, string? LastName);
}
