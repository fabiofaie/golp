using System.Security.Claims;
using Golp.Api.Services;

namespace Golp.Tests.Services;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void GetImpersonatorId_ValidClaim_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var user = PrincipalWith(new Claim("impersonator_id", id.ToString()));

        Assert.Equal(id, user.GetImpersonatorId());
    }

    [Fact]
    public void GetImpersonatorId_MissingClaim_ReturnsNull()
    {
        var user = PrincipalWith();

        Assert.Null(user.GetImpersonatorId());
    }

    [Fact]
    public void GetImpersonatorId_MalformedClaim_ReturnsNull()
    {
        var user = PrincipalWith(new Claim("impersonator_id", "not-a-guid"));

        Assert.Null(user.GetImpersonatorId());
    }
}
