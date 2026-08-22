using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TicketSpan.Api.Data;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Security;
using TicketSpan.Api.Services;
using TicketSpan.Protos.Auth;

namespace TicketSpan.Api.Endpoints.V1;

public static class AuthEndpointsV1
{
    public static RouteGroupBuilder MapAuthApiV1(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth").WithTags("Authentication");

        auth.MapPost("/login", async (
            LoginApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.Login(new LoginRequest
                {
                    Email = request.Email,
                    Password = request.Password,
                    TenantSlug = request.TenantSlug ?? string.Empty,
                    Portal = request.Portal ?? string.Empty
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new AuthApiResponse(
                    response.AccessToken,
                    response.RefreshToken,
                    response.ExpiresAt,
                    response.User.UsersId,
                    response.User.TenantsId,
                    response.User.Email,
                    response.User.FirstName,
                    response.User.LastName,
                    response.User.Role,
                    response.User.TenantSlug,
                    response.User.EmailVerified));
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
            {
                return Results.Unauthorized();
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    (int)Grpc.Core.StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
                    (int)Grpc.Core.StatusCode.FailedPrecondition => StatusCodes.Status412PreconditionFailed,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        auth.MapPost("/signup", async (
            SignUpApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.SignUp(new SignUpRequest
                {
                    Email = request.Email,
                    Password = request.Password,
                    FirstName = request.FirstName ?? string.Empty,
                    LastName = request.LastName ?? string.Empty,
                    TenantSlug = request.TenantSlug
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new AuthApiResponse(
                    response.AccessToken,
                    response.RefreshToken,
                    response.ExpiresAt,
                    response.User.UsersId,
                    response.User.TenantsId,
                    response.User.Email,
                    response.User.FirstName,
                    response.User.LastName,
                    response.User.Role,
                    response.User.TenantSlug,
                    response.User.EmailVerified));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    (int)Grpc.Core.StatusCode.AlreadyExists => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        auth.MapPost("/google", async (
            GoogleAuthApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.GoogleSignIn(new GoogleSignInRequest
                {
                    GoogleToken = request.GoogleToken,
                    TenantSlug = request.TenantSlug ?? string.Empty,
                    Portal = request.Portal ?? string.Empty
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new AuthApiResponse(
                    response.AccessToken,
                    response.RefreshToken,
                    response.ExpiresAt,
                    response.User.UsersId,
                    response.User.TenantsId,
                    response.User.Email,
                    response.User.FirstName,
                    response.User.LastName,
                    response.User.Role,
                    response.User.TenantSlug,
                    response.User.EmailVerified));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: (int)ex.StatusCode switch
                {
                    (int)Grpc.Core.StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
                    (int)Grpc.Core.StatusCode.InvalidArgument => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status500InternalServerError
                });
            }
        }).AllowAnonymous();

        auth.MapPost("/magic-link/request", async (
            MagicLinkRequestApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.RequestMagicLink(new MagicLinkRequest
                {
                    Email = request.Email,
                    TenantSlug = request.TenantSlug ?? string.Empty,
                    Origin = request.Portal ?? string.Empty
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new { success = response.Success, message = response.Message });
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        auth.MapPost("/magic-link/verify", async (
            VerifyMagicLinkApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.VerifyMagicLink(new MagicLinkVerifyRequest
                {
                    Token = request.Token
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new AuthApiResponse(
                    response.AccessToken,
                    response.RefreshToken,
                    response.ExpiresAt,
                    response.User.UsersId,
                    response.User.TenantsId,
                    response.User.Email,
                    response.User.FirstName,
                    response.User.LastName,
                    response.User.Role,
                    response.User.TenantSlug,
                    response.User.EmailVerified));
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status401Unauthorized);
            }
        }).AllowAnonymous();

        auth.MapPost("/password-reset/request", async (
            PasswordResetRequestApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.RequestPasswordReset(new PasswordResetRequest
                {
                    Email = request.Email,
                    TenantSlug = request.TenantSlug ?? string.Empty
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new { success = response.Success, message = response.Message });
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        auth.MapPost("/password-reset/confirm", async (
            PasswordResetConfirmApiRequest request,
            AuthServiceImpl authService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await authService.SetPassword(new SetPasswordRequest
                {
                    Token = request.Token,
                    NewPassword = request.NewPassword
                }, new UnaryServerCallContext(ct));

                return Results.Ok(new { success = response.Success, message = response.Message });
            }
            catch (Grpc.Core.RpcException ex)
            {
                return Results.Problem(detail: ex.Status.Detail, statusCode: StatusCodes.Status400BadRequest);
            }
        }).AllowAnonymous();

        auth.MapGet("/me", async (
            ClaimsPrincipal user,
            TenantContext tenantContext,
            Db db,
            CancellationToken ct) =>
        {
            if (tenantContext.UsersId is not { } usersId)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(usersId, tenantContext.TenantsId, ct);
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT users_id, tenants_id, email, first_name, last_name, role, email_verified FROM vw_users WHERE users_id = @id",
                connection);
            cmd.Parameters.AddWithValue("id", usersId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound();
            }

            var profile = new UserProfileApiResponse(
                reader.GetGuid(0).ToString(),
                reader.IsDBNull(1) ? null : reader.GetGuid(1).ToString(),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt16(5),
                reader.GetBoolean(6));

            return Results.Ok(profile);
        }).RequireAuthorization();

        return group;
    }
}

public sealed record LoginApiRequest(string Email, string Password, string? TenantSlug, string? Portal);
public sealed record SignUpApiRequest(string Email, string Password, string FirstName, string LastName, string TenantSlug);
public sealed record GoogleAuthApiRequest(string GoogleToken, string? TenantSlug, string? Portal);
public sealed record MagicLinkRequestApiRequest(string Email, string? TenantSlug, string? Portal);
public sealed record VerifyMagicLinkApiRequest(string Token);
public sealed record PasswordResetRequestApiRequest(string Email, string? TenantSlug);
public sealed record PasswordResetConfirmApiRequest(string Token, string NewPassword);

public sealed record AuthApiResponse(
    string AccessToken,
    string RefreshToken,
    long ExpiresAt,
    string UsersId,
    string? TenantsId,
    string Email,
    string FirstName,
    string LastName,
    int Role,
    string TenantSlug,
    bool EmailVerified);

public sealed record UserProfileApiResponse(
    string UsersId,
    string? TenantsId,
    string Email,
    string? FirstName,
    string? LastName,
    int Role,
    bool EmailVerified);
