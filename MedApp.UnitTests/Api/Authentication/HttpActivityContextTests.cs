using System.Net;
using System.Security.Claims;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using Microsoft.AspNetCore.Http;
using Moq;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Authentication;

[TestFixture]
public sealed class HttpActivityContextTests
{
    private static DefaultHttpContext Http(ClaimsPrincipal user)
    {
        var http = new DefaultHttpContext { User = user };
        http.Connection.RemoteIpAddress = IPAddress.Parse(IpAddress);
        return http;
    }

    [Test]
    public void From_SignedIn_IsTheUserAndTheirAddress()
    {
        var actor = HttpActivityContext.From(Http(SignedIn("User")));

        actor.UserId.Should().Be(UserId);
        actor.Username.Should().Be(Username);
        actor.IpAddress.Should().Be(IpAddress);
    }

    [Test]
    public void From_Anonymous_HasNoUser_ButTheAddress()
    {
        var actor = HttpActivityContext.From(Http(new ClaimsPrincipal(new ClaimsIdentity())));

        actor.UserId.Should().BeNull();
        actor.Username.Should().BeEmpty();
        actor.IpAddress.Should().Be(IpAddress);
    }

    [Test]
    public void From_BadIdClaim_HasNoUserId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "not-a-guid"), new Claim(ClaimTypes.Name, Username)], "Session"));

        var actor = HttpActivityContext.From(Http(user));

        actor.UserId.Should().BeNull();
        actor.Username.Should().Be(Username);
    }

    [Test]
    public void From_SignedInWithoutAName_HasAnEmptyName()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "Session"));

        HttpActivityContext.From(new DefaultHttpContext { User = user }).Username.Should().BeEmpty();
    }

    [Test]
    public void From_NoRequest_IsNobody()
    {
        var actor = HttpActivityContext.From(null);

        actor.UserId.Should().BeNull();
        actor.Username.Should().BeEmpty();
        actor.IpAddress.Should().BeNull();
    }

    [Test]
    public void Actor_IsTheCurrentRequests()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(Http(SignedIn("Admin")));

        new HttpActivityContext(accessor.Object).Actor.UserId.Should().Be(UserId);
    }
}
