using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Endpoints.Common;
using TicketSpan.Api.Payments;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.Endpoints.V1;

public static class PaymentEndpointsV1
{
    public static RouteGroupBuilder MapPaymentApiV1(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments").WithTags("Payments");

        payments.MapGet("/saved-methods", async (
            Db db,
            TenantContext tenantContext,
            StripeService stripe,
            CancellationToken ct) =>
        {
            if (tenantContext.UsersId is null)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
            string? stripeCustomerId = null;
            await using (var cmd = new NpgsqlCommand(
                "SELECT stripe_customer_id FROM users WHERE users_id = @u", connection))
            {
                cmd.Parameters.AddWithValue("u", tenantContext.UsersId.Value);
                stripeCustomerId = await cmd.ExecuteScalarAsync(ct) as string;
            }

            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                return Results.Ok(new ApiEnvelope<IReadOnlyList<SavedPaymentMethodDto>>(true, Array.Empty<SavedPaymentMethodDto>()));
            }

            try
            {
                var methods = await stripe.ListPaymentMethodsAsync(stripeCustomerId, ct);
                var items = methods.Data.Select(pm => new SavedPaymentMethodDto(
                    pm.Id,
                    pm.Card?.Brand ?? "card",
                    pm.Card?.Last4 ?? "****",
                    (int)(pm.Card?.ExpMonth ?? 0),
                    (int)(pm.Card?.ExpYear ?? 0),
                    pm.Card?.Funding ?? "credit"
                )).ToList();

                return Results.Ok(new ApiEnvelope<IReadOnlyList<SavedPaymentMethodDto>>(true, items));
            }
            catch (Stripe.StripeException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        payments.MapPost("/setup-intent", async (
            Db db,
            TenantContext tenantContext,
            StripeService stripe,
            CancellationToken ct) =>
        {
            if (tenantContext.UsersId is null)
            {
                return Results.Unauthorized();
            }

            await using var connection = await db.OpenAsync(tenantContext.UsersId, tenantContext.TenantsId, ct);
            string? stripeCustomerId = null;
            string userEmail = "";
            string userName = "";
            await using (var cmd = new NpgsqlCommand(
                "SELECT email, first_name || ' ' || last_name, stripe_customer_id FROM users WHERE users_id = @u", connection))
            {
                cmd.Parameters.AddWithValue("u", tenantContext.UsersId.Value);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    userEmail = reader.GetString(0);
                    userName = reader.GetString(1);
                    stripeCustomerId = reader.IsDBNull(2) ? null : reader.GetString(2);
                }
            }

            if (string.IsNullOrEmpty(stripeCustomerId))
            {
                stripeCustomerId = await stripe.GetOrCreateCustomerAsync(null, userEmail, userName, tenantContext.UsersId.Value, ct);
                await using var saveCmd = new NpgsqlCommand(
                    "SELECT sp_get_or_set_user_stripe_customer(@u, @c)", connection);
                saveCmd.Parameters.AddWithValue("u", tenantContext.UsersId.Value);
                saveCmd.Parameters.AddWithValue("c", stripeCustomerId);
                await saveCmd.ExecuteScalarAsync(ct);
            }

            try
            {
                var setupIntent = await stripe.CreateSetupIntentAsync(stripeCustomerId, ct);
                return Results.Ok(new ApiEnvelope<SetupIntentApiResponse>(true, new SetupIntentApiResponse(
                    setupIntent.ClientSecret,
                    stripe.PublishableKey,
                    stripeCustomerId
                )));
            }
            catch (Stripe.StripeException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        payments.MapDelete("/saved-methods/{paymentMethodId}", async (
            string paymentMethodId,
            Db db,
            TenantContext tenantContext,
            StripeService stripe,
            CancellationToken ct) =>
        {
            if (tenantContext.UsersId is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(paymentMethodId))
            {
                return Results.BadRequest(new AckEnvelope(false, "Invalid payment method ID", 400));
            }

            try
            {
                await stripe.DetachPaymentMethodAsync(paymentMethodId.Trim(), ct);
                return Results.Ok(new AckEnvelope(true, "Payment method detached successfully", 200));
            }
            catch (Stripe.StripeException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization();

        return group;
    }
}

public sealed record SavedPaymentMethodDto(
    string Id,
    string Brand,
    string Last4,
    int ExpMonth,
    int ExpYear,
    string Funding);

public sealed record SetupIntentApiResponse(
    string ClientSecret,
    string PublishableKey,
    string CustomerId);
