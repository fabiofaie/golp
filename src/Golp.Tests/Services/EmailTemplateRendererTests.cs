using Golp.Api.Services;

namespace Golp.Tests.Services;

public class EmailTemplateRendererTests : IDisposable
{
    private readonly string _tempDir;

    public EmailTemplateRendererTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"golp-email-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        File.WriteAllText(Path.Combine(_tempDir, "_layout.html"), "<html><body>{{Body}}</body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "greeting.html"), "<p>Ciao {{Name}}, benvenuto in {{Circle}}.</p>");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Render_SubstitutesPlaceholders_AndInjectsIntoLayout()
    {
        var renderer = new EmailTemplateRenderer(_tempDir);

        var html = renderer.Render("greeting", new Dictionary<string, string>
        {
            ["Name"] = "Marco",
            ["Circle"] = "Padel Club Roma",
        });

        Assert.Contains("<html><body>", html);
        Assert.Contains("Ciao Marco, benvenuto in Padel Club Roma.", html);
    }

    [Fact]
    public void Render_MissingPlaceholderValue_RemainsLiteral_NoException()
    {
        var renderer = new EmailTemplateRenderer(_tempDir);

        var html = renderer.Render("greeting", new Dictionary<string, string>
        {
            ["Name"] = "Marco",
        });

        Assert.Contains("{{Circle}}", html);
    }

    [Fact]
    public void Render_UnknownTemplate_ThrowsFileNotFoundException()
    {
        var renderer = new EmailTemplateRenderer(_tempDir);

        Assert.Throws<FileNotFoundException>(() =>
            renderer.Render("does-not-exist", new Dictionary<string, string>()));
    }
}
