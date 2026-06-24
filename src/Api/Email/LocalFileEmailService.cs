using System.Text;

namespace Svyne.Api.Email;

public sealed class LocalFileEmailService : IEmailService
{
    private readonly string outputDir;
    private readonly ILogger<LocalFileEmailService> logger;

    public LocalFileEmailService(IConfiguration configuration, ILogger<LocalFileEmailService> logger)
    {
        outputDir = configuration["LOCAL_EMAIL_DIR"] ?? string.Empty;
        this.logger = logger;
    }

    public async Task SendAsync(string fromAddress, string toAddress, string subject, string htmlBody, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            logger.LogInformation("Email to {To} suppressed (no LOCAL_EMAIL_DIR configured): {Subject}", toAddress, subject);
            return;
        }

        Directory.CreateDirectory(outputDir);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var safeRecipient = string.Concat(toAddress.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{timestamp}_{safeRecipient}.html";
        var path = Path.Combine(outputDir, fileName);

        var builder = new StringBuilder();
        builder.AppendLine($"<!-- From: {fromAddress} -->");
        builder.AppendLine($"<!-- To: {toAddress} -->");
        builder.AppendLine($"<!-- Subject: {subject} -->");
        builder.AppendLine($"<!-- Sent: {DateTime.UtcNow:O} -->");
        builder.Append(htmlBody);

        await File.WriteAllTextAsync(path, builder.ToString(), ct);
        logger.LogInformation("Email written to {Path}", path);
    }
}
