using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TicketSpan.Api.Data;
using TicketSpan.Api.Security;

namespace TicketSpan.Api.Email;

public sealed class LoggingEmailService : IEmailService
{
    private readonly IEmailService inner;
    private readonly Db db;
    private readonly IServiceProvider serviceProvider;

    public LoggingEmailService(IEmailService inner, Db db, IServiceProvider serviceProvider)
    {
        this.inner = inner;
        this.db = db;
        this.serviceProvider = serviceProvider;
    }

    public async Task SendAsync(string fromAddress, string toAddress, string subject, string htmlBody, CancellationToken ct)
    {
        string status = "sent";
        try
        {
            await inner.SendAsync(fromAddress, toAddress, subject, htmlBody, ct);
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            try
            {
                Guid? tenantId = null;
                // Try to resolve TenantContext if in HttpContext scope
                using (var scope = serviceProvider.CreateScope())
                {
                    var httpContextAccessor = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                    if (httpContextAccessor?.HttpContext?.RequestServices.GetService<TenantContext>() is { } tc)
                    {
                        tenantId = tc.TenantsId;
                    }
                }

                await using var connection = await db.OpenAsync(null, null, ct);
                await using var cmd = new NpgsqlCommand(
                    "SELECT sp_log_email(@tenantId, @recipient, @subject, @body, @status)", connection);

                cmd.Parameters.AddWithValue("tenantId", (object?)tenantId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("recipient", toAddress);
                cmd.Parameters.AddWithValue("subject", subject);
                cmd.Parameters.AddWithValue("body", htmlBody);
                cmd.Parameters.AddWithValue("status", status);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                // Log but don't fail the mail send if db log fails
                Console.WriteLine($"Failed to write email log to database: {ex}");
            }
        }
    }
}
