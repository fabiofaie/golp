using System.Security.Claims;

namespace Golp.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.FindFirstValue("super_admin") == "true";

    public static Guid? GetImpersonatorId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue("impersonator_id"), out var id) ? id : null;
}
