using Microsoft.Extensions.Hosting;

namespace TicketSpan.Api.Email;

public sealed class EmailTemplateRenderer
{
    private readonly string templateRoot;

    public EmailTemplateRenderer(IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            
            var current = Directory.GetCurrentDirectory();
            var candidates = new[]
            {
                Path.Combine(current, "src", "Api", "Email", "Templates"),
                Path.Combine(current, "Email", "Templates"),
                Path.Combine(current, "..", "src", "Api", "Email", "Templates")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    templateRoot = Path.GetFullPath(candidate);
                    return;
                }
            }
        }

        templateRoot = Path.Combine(AppContext.BaseDirectory, "Email", "Templates");
    }

    public async Task<string> RenderAsync(string templateName, IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var path = Path.Combine(templateRoot, templateName);
        var template = await File.ReadAllTextAsync(path, ct);
        foreach (var (key, value) in values)
        {
            template = template.Replace("{{" + key + "}}", value);
        }
        return template;
    }
}
