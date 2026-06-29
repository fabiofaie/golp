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

    [Fact]
    public void Render_AwardWinnerTemplate_ContainsAllValues()
    {
        File.WriteAllText(Path.Combine(_tempDir, "award-winner.html"),
            "<p>Ciao {{WinnerName}}! Hai vinto il {{HumanPeriodLabel}} in {{CircleName}}. Gain: {{NetGain}}, Partite: {{MatchesPlayed}}.</p>");

        var renderer = new EmailTemplateRenderer(_tempDir);

        var html = renderer.Render("award-winner", new Dictionary<string, string>
        {
            ["WinnerName"]       = "Marco",
            ["HumanPeriodLabel"] = "Giugno 2026",
            ["CircleName"]       = "Padel Club Roma",
            ["NetGain"]          = "42",
            ["MatchesPlayed"]    = "7",
        });

        Assert.Contains("Marco", html);
        Assert.Contains("Giugno 2026", html);
        Assert.Contains("Padel Club Roma", html);
        Assert.Contains("42", html);
        Assert.Contains("7", html);
    }
}
