namespace Golp.Api.Services;

public class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly string _templatesDir;

    public EmailTemplateRenderer(string? templatesDir = null)
    {
        _templatesDir = templatesDir ?? Path.Combine(AppContext.BaseDirectory, "EmailTemplates");
    }

    public string Render(string templateName, Dictionary<string, string> values)
    {
        var layout = LoadFile("_layout.html");
        var body = LoadFile($"{templateName}.html");

        body = Substitute(body, values);
        var merged = layout.Replace("{{Body}}", body);
        return Substitute(merged, values);
    }

    private string LoadFile(string fileName)
    {
        var path = Path.Combine(_templatesDir, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Email template non trovato: {path}");

        return File.ReadAllText(path);
    }

    private static string Substitute(string content, Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
            content = content.Replace($"{{{{{key}}}}}", value);

        return content;
    }
}
