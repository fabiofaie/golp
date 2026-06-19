namespace Golp.Api.Services;

public interface IEmailTemplateRenderer
{
    string Render(string templateName, Dictionary<string, string> values);
}
