using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class AdminEndpointsTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AdminEndpointsTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"admin-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task WhoAmI_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_NormalUser_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_SuperAdmin_Returns200()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Admin", email, password = "password123" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsSuperAdmin = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, responseBody.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Impersonate_EmptyEmail_Returns400()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_NormalUser_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_UnknownEmail_Returns404()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_ExistingUser_Returns200WithImpersonatorClaim()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = body.GetProperty("token").GetString();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(impersonationToken);

        Assert.Equal(targetEmail, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(adminId.ToString(), jwt.Claims.First(c => c.Type == "impersonator_id").Value);
    }

    [Fact]
    public async Task Impersonate_Success_CreatesOpenAuditLog()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = await db.Users.FirstAsync(u => u.Email == targetEmail);
        var log = await db.ImpersonationAuditLogs
            .FirstOrDefaultAsync(l => l.SuperAdminId == adminId && l.TargetUserId == target.Id);

        Assert.NotNull(log);
        Assert.Null(log!.EndedAt);
    }

    [Fact]
    public async Task EndImpersonation_NoImpersonatorClaim_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EndImpersonation_ClosesOpenAuditLog()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var impersonateResponse = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });
        var impersonateBody = await impersonateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = impersonateBody.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", impersonationToken);
        var endResponse = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = await db.Users.FirstAsync(u => u.Email == targetEmail);
        var log = await db.ImpersonationAuditLogs
            .FirstAsync(l => l.SuperAdminId == adminId && l.TargetUserId == target.Id);

        Assert.NotNull(log.EndedAt);
    }

    [Fact]
    public async Task EndImpersonation_CalledTwice_IsIdempotent()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var impersonateResponse = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });
        var impersonateBody = await impersonateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = impersonateBody.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", impersonationToken);
        await _client.PostAsync("/admin/impersonate/end", null);
        var secondEnd = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.OK, secondEnd.StatusCode);
    }

    private async Task<string> RegisterSuperAdminAndLoginAsync(Action<Guid>? captureId = null)
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Admin", email, password = "password123" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsSuperAdmin = true;
            await db.SaveChangesAsync();
            captureId?.Invoke(user.Id);
        }

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}
