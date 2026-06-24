namespace Svyne.Api.Email;

public sealed class EmailTemplateRenderer
{
    private readonly string templateRoot;

    public EmailTemplateRenderer()
    {
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
