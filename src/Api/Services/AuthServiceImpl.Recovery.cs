using Google.Apis.Auth;
using Grpc.Core;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Email;
using TicketSpan.Api.Security;
using TicketSpan.Protos.Auth;

namespace TicketSpan.Api.Services;

public sealed partial class AuthServiceImpl
{
    public override async Task<TicketSpan.Protos.Common.AckResponse> RequestMagicLink(MagicLinkRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var tenantsId = await ResolveTenantAsync(request.TenantSlug, ct);
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_create_magic_link(@email, @hash, @exp, @t)", connection);
        cmd.Parameters.AddWithValue("email", request.Email);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddMinutes(15));
        cmd.Parameters.AddWithValue("t", (object?)tenantsId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "Magic link sent" };
    }

    public override async Task<AuthResponse> VerifyMagicLink(MagicLinkVerifyRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        string email;
        Guid? linkTenant;
        await using (var connection = await db.OpenAsync(null, null, ct))
        await using (var cmd = new NpgsqlCommand("SELECT email, tenants_id FROM sp_consume_magic_link(@h)", connection))
        {
            cmd.Parameters.AddWithValue("h", hash);
            await using var consumeReader = await cmd.ExecuteReaderAsync(ct);
            if (!await consumeReader.ReadAsync(ct))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired link"));
            }
            email = consumeReader.GetString(0);
            linkTenant = consumeReader.IsDBNull(1) ? (Guid?)null : consumeReader.GetGuid(1);
        }
        var emailHash = EmailHasher.Hash(email);
        await using var conn = await db.OpenAsync(null, null, ct);
        await using var lookup = new NpgsqlCommand(
            "SELECT users_id, tenants_id, role, email, first_name, last_name, email_verified "
            + "FROM sp_get_user_by_email_hash(@h) WHERE is_active = true AND tenants_id IS NOT DISTINCT FROM @tenant LIMIT 1", conn);
        lookup.Parameters.AddWithValue("h", emailHash);
        lookup.Parameters.AddWithValue("tenant", (object?)linkTenant ?? DBNull.Value);

        Guid usersId;
        Guid? rowTenant;
        short role;
        string firstName;
        string lastName;
        bool emailVerified;
        await using (var reader = await lookup.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                usersId = reader.GetGuid(0);
                rowTenant = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);
                role = reader.GetInt16(2);
                firstName = reader.GetString(4);
                lastName = reader.GetString(5);
                emailVerified = reader.GetBoolean(6);
            }
            else if (linkTenant is { } tenant)
            {
                await reader.CloseAsync();
                (usersId, rowTenant, role, firstName, lastName, emailVerified) =
                    await CreateAttendeeAsync(conn, tenant, email, emailHash, ct);
            }
            else
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User not found"));
            }
        }

        var profile = new UserProfile
        {
            UsersId = usersId.ToString(),
            TenantsId = rowTenant?.ToString() ?? string.Empty,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            EmailVerified = emailVerified
        };
        return BuildAuth(usersId, email, rowTenant, role, string.Empty, profile);
    }

    public override async Task<TicketSpan.Protos.Common.AckResponse> RequestPasswordReset(PasswordResetRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var emailHash = EmailHasher.Hash(request.Email);
        await using var connection = await db.OpenAsync(null, null, ct);
        Guid usersId;
        await using (var lookup = new NpgsqlCommand("SELECT users_id FROM sp_get_user_by_email_hash(@h) LIMIT 1", connection))
        {
            lookup.Parameters.AddWithValue("h", emailHash);
            var result = await lookup.ExecuteScalarAsync(ct);
            if (result is not Guid u)
            {
                return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "If the account exists, a reset link was sent" };
            }
            usersId = u;
        }
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var hash = EmailHasher.Hash(token);
        var expiryHours = await settings.GetIntAsync("password_reset_expiry_hours", 1, ct);
        await using var cmd = new NpgsqlCommand("SELECT sp_create_password_reset_token(@u, @h, @exp, @email, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddHours(expiryHours));
        cmd.Parameters.AddWithValue("email", request.Email);
        await cmd.ExecuteNonQueryAsync(ct);

        try
        {
            var fromAddress = await settings.GetStringAsync("password_reset_email", "noreply@ticketspan.com", ct);
            var subject = await settings.GetStringAsync("password_reset_subject", "Reset your TicketSpan password", ct);
            string resetBase;
            if (!string.IsNullOrEmpty(request.Origin))
            {
                resetBase = $"{request.Origin.TrimEnd('/')}/set-password";
            }
            else
            {
                var template = await settings.GetStringAsync("password_reset_link_base", "http://{slug}.localhost:5173/set-password", ct);
                resetBase = string.IsNullOrEmpty(request.TenantSlug)
                    ? template.Replace("{slug}.", string.Empty).Replace("{slug}", string.Empty)
                    : template.Replace("{slug}", request.TenantSlug);
            }
            var separator = resetBase.Contains('?') ? "&" : "?";
            var resetLink = $"{resetBase}{separator}token={token}";
            if (!string.IsNullOrEmpty(request.TenantSlug))
            {
                resetLink += $"&tenant={Uri.EscapeDataString(request.TenantSlug)}";
            }
            var values = new Dictionary<string, string>
            {
                ["Subject"] = subject,
                ["Email"] = request.Email,
                ["ResetLink"] = resetLink,
                ["ExpiryHours"] = expiryHours.ToString()
            };
            var htmlBody = await templates.RenderAsync("password_reset.html", values, ct);
            await email.SendAsync(fromAddress, request.Email, subject, htmlBody, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email");
        }

        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "If the account exists, a reset link was sent" };
    }

    public override async Task<TicketSpan.Protos.Common.AckResponse> ValidatePasswordResetToken(ValidateResetTokenRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(null, null, ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT is_used, expires_at FROM sp_get_password_reset_token(@h)", connection);
        cmd.Parameters.AddWithValue("h", hash);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Invalid token. Please request a new password reset link."));
        }
        var isUsed = reader.GetBoolean(0);
        var expiresAt = reader.GetDateTime(1);
        if (isUsed || expiresAt <= DateTime.UtcNow)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "This reset link has already been used or has expired. Please request a new one."));
        }
        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "Token valid" };
    }

    public override async Task<TicketSpan.Protos.Common.AckResponse> SetPassword(SetPasswordRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var hash = EmailHasher.Hash(request.Token);
        await using var connection = await db.OpenAsync(null, null, ct);
        Guid usersId;
        await using (var consume = new NpgsqlCommand("SELECT users_id FROM sp_consume_password_reset_token(@h)", connection))
        {
            consume.Parameters.AddWithValue("h", hash);
            var result = await consume.ExecuteScalarAsync(ct);
            if (result is not Guid u)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired token"));
            }
            usersId = u;
        }
        var newHash = passwordHasher.Hash(request.NewPassword);
        await using var cmd = new NpgsqlCommand("SELECT sp_set_user_password(@u, @h, @pv, true, NULL)", connection);
        cmd.Parameters.AddWithValue("u", usersId);
        cmd.Parameters.AddWithValue("h", newHash);
        cmd.Parameters.AddWithValue("pv", passwordHasher.CurrentVersion);
        await cmd.ExecuteNonQueryAsync(ct);
        return new TicketSpan.Protos.Common.AckResponse { Success = true, Message = "Password updated" };
    }
}
