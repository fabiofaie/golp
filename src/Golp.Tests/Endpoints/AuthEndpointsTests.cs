using System.Net;
using System.Net.Http.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Endpoints;

public class AuthEndpointsTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(AuthTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Test", email = "test@example.com", password = "1234567" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var payload = new { name = "User", email = "dup@example.com", password = "password123" };

        var first = await _client.PostAsJsonAsync("/auth/register", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/auth/register", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email = "wrongpw@example.com", password = "correctpassword" });

        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email = "wrongpw@example.com", password = "wrongpassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email = "nobody@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public class AuthTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
}
