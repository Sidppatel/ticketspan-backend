using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TicketSpan.Api.Email;

public sealed class ResendEmailService : IEmailService
{
    private static readonly HttpClient http = new() { BaseAddress = new Uri("https://api.resend.com") };
    private readonly string apiKey;
    private readonly ILogger<ResendEmailService> logger;

    public ResendEmailService(IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        apiKey = configuration["RESEND_API_KEY"] ?? string.Empty;
        this.logger = logger;
    }

    public async Task SendAsync(string fromAddress, string toAddress, string subject, string htmlBody, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/emails")
        {
            Content = JsonContent.Create(new { from = fromAddress, to = new[] { toAddress }, subject, html = htmlBody }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Resend send failed ({(int)response.StatusCode}): {body}");
        }

        logger.LogInformation("Email sent to {To} via Resend: {Subject}", toAddress, subject);
    }
}
