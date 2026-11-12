using System.Security.Claims;
using Birko.Security.AspNetCore;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>TASK-134: the caller a <c>/dragon</c> or <c>/kobold</c> connection runs as, read through <see cref="ICurrentUser"/>.</summary>
public class WebSocketCallerTests
{
    private static ICurrentUser User(params Claim[] claims) => User(authenticated: true, claims);

    private static ICurrentUser User(bool authenticated, params Claim[] claims)
    {
        var identity = authenticated ? new ClaimsIdentity(claims, "Bearer") : new ClaimsIdentity(claims);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        // Program.cs reads permissions from the "scope" claim.
        return new ClaimsCurrentUser(new HttpContextAccessor { HttpContext = context },
            new ClaimMappingOptions { PermissionClaim = "scope" });
    }

    [Theory]
    [InlineData("github:4242")]
    [InlineData("service:ci")]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    public void The_subject_is_the_raw_claim_whether_or_not_it_is_a_guid(string sub)
    {
        var caller = WebSocketCaller.From(User(new Claim(ClaimTypes.NameIdentifier, sub), new Claim("scope", KoboldLairPermissionChecker.ViewOwn)));

        caller.Sub.Should().Be(sub);
        caller.IsAdmin.Should().BeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData(KoboldLairPermissionChecker.ViewAll)]
    [InlineData(KoboldLairPermissionChecker.ViewOwn + " " + KoboldLairPermissionChecker.ViewAll)]
    public void Wildcard_or_view_all_makes_the_caller_an_admin(string scope)
    {
        WebSocketCaller.From(User(new Claim(ClaimTypes.NameIdentifier, "github:1"), new Claim("scope", scope)))
            .IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void Name_and_email_prefer_the_short_claims_and_fall_back_to_the_mapped_ones()
    {
        WebSocketCaller.From(User(
                new Claim(ClaimTypes.NameIdentifier, "github:1"),
                new Claim("name", "Short"), new Claim(ClaimTypes.Name, "Mapped"),
                new Claim("email", "short@example.test"), new Claim(ClaimTypes.Email, "mapped@example.test")))
            .Should().Be(new Server.Services.KoboldCaller("github:1", false, "Short", "short@example.test"));

        WebSocketCaller.From(User(
                new Claim(ClaimTypes.NameIdentifier, "github:1"),
                new Claim(ClaimTypes.Name, "Mapped"), new Claim(ClaimTypes.Email, "mapped@example.test")))
            .Should().Be(new Server.Services.KoboldCaller("github:1", false, "Mapped", "mapped@example.test"));
    }

    [Fact]
    public void Without_an_authenticated_user_the_caller_is_the_local_owner()
    {
        WebSocketCaller.From(User(authenticated: false))
            .Should().Be(new Server.Services.KoboldCaller(null, true, null, null));
    }
}
