namespace TicketSpan.Api.Email;

public interface IEmailService
{
    Task SendAsync(string fromAddress, string toAddress, string subject, string htmlBody, CancellationToken ct);
}
