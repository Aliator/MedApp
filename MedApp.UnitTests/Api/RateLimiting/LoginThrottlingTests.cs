using System.Net;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.RateLimiting;
using MedApp.Contracts.Authentication.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.RateLimiting;

[TestFixture]
public sealed class LoginThrottlingTests
{
    private const string OtherIpAddress = "192.0.2.20";

    private ServiceProvider _services = null!;
    private RequestDelegate _pipeline = null!;

    [SetUp]
    public void SetUp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginThrottling:PerClientPermitLimit"] = "2",
                ["LoginThrottling:PerUsernamePermitLimit"] = "2",
                ["LoginThrottling:Window"] = "00:01:00",
            })
            .Build();
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddLoginThrottling()
            .BuildServiceProvider();

        // The rate limiting middleware in front of an endpoint with the sign-in policy, as the API has it.
        var app = new ApplicationBuilder(_services);
        app.UseRateLimiter();
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        _pipeline = app.Build();
    }

    [TearDown]
    public void TearDown() => _services.Dispose();

    private async Task<HttpContext> SignIn(string ipAddress = IpAddress)
    {
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new EnableRateLimitingAttribute(LoginThrottling.Policy)), "login"));
        await _pipeline(context);
        return context;
    }

    private static ProblemDetails Problem(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonSerializer.Deserialize<ProblemDetails>(context.Response.Body, JsonSerializerOptions.Web)!;
    }

    [Test]
    public async Task SignIn_WithinTheClientLimit_IsAllowed()
    {
        (await SignIn()).Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        (await SignIn()).Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task SignIn_OverTheClientLimit_IsRefusedWithRetryAfter()
    {
        await SignIn();
        await SignIn();

        var refused = await SignIn();

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        refused.Response.ContentType.Should().StartWith("application/problem+json");
        var seconds = int.Parse(refused.Response.Headers.RetryAfter.ToString());
        seconds.Should().BeInRange(1, 60);
        var problem = Problem(refused);
        problem.Status.Should().Be(StatusCodes.Status429TooManyRequests);
        problem.Title.Should().StartWith("Too many sign-in attempts.");
    }

    [Test]
    public async Task SignIn_FromAnotherClient_HasItsOwnAllowance()
    {
        await SignIn();
        await SignIn();

        (await SignIn(OtherIpAddress)).Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [TestCase(null, "unknown")]
    [TestCase("192.0.2.10", "192.0.2.10")]
    [TestCase("::ffff:192.0.2.10", "192.0.2.10")]
    [TestCase("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [TestCase("2001:db8:1:2:ffff:ffff:ffff:ffff", "2001:db8:1:2::/64")]
    public void ClientKey_CountsIPv6ByItsNetwork(string? address, string expected)
    {
        LoginThrottling.ClientKey(address is null ? null : IPAddress.Parse(address)).Should().Be(expected);
    }

    [TestCase(1, "Too many sign-in attempts. Try again in 1 second.")]
    [TestCase(42, "Too many sign-in attempts. Try again in 42 seconds.")]
    public void TooManyAttempts_SaysHowLongToWait(int seconds, string expected)
    {
        LoginThrottling.TooManyAttempts(seconds).Title.Should().Be(expected);
    }

    [Test]
    public void RetryAfterSeconds_NoneFromTheLimiter_IsTheWindow()
    {
        using var lease = new System.Threading.RateLimiting.ConcurrencyLimiter(
            new System.Threading.RateLimiting.ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 }).AttemptAcquire();

        LoginThrottling.RetryAfterSeconds(lease, TimeSpan.FromSeconds(90)).Should().Be(90);
    }

    [Test]
    public void Options_Defaults_AreThirtyPerClientAndFivePerUsernameAMinute()
    {
        var defaults = new LoginThrottlingOptions();

        defaults.PerClientPermitLimit.Should().Be(30);
        defaults.PerUsernamePermitLimit.Should().Be(5);
        defaults.Window.Should().Be(TimeSpan.FromMinutes(1));
    }

    private async Task<(ActionExecutingContext Context, bool Ran)> ThrottleByUsername(LoginRequest? request)
    {
        var http = new DefaultHttpContext { RequestServices = _services };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var arguments = new Dictionary<string, object?>();
        if (request is not null) arguments["request"] = request;
        var context = new ActionExecutingContext(actionContext, [], arguments, controller: new object());
        var ran = false;

        await new ThrottleLoginByUsernameAttribute().OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        });
        return (context, ran);
    }

    [Test]
    public async Task ThrottleByUsername_WithinTheLimit_RunsTheAction()
    {
        var (_, first) = await ThrottleByUsername(new LoginRequest(Username, Password));
        var (_, second) = await ThrottleByUsername(new LoginRequest(Username, Password));

        first.Should().BeTrue();
        second.Should().BeTrue();
    }

    [Test]
    public async Task ThrottleByUsername_OverTheLimit_IsRefusedWhateverTheCaseOrSpaces()
    {
        await ThrottleByUsername(new LoginRequest(Username, Password));
        await ThrottleByUsername(new LoginRequest(Username.ToUpperInvariant(), Password));

        var (context, ran) = await ThrottleByUsername(new LoginRequest($" {Username} ", Password));

        ran.Should().BeFalse();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        result.Value.Should().BeOfType<ProblemDetails>().Which.Title.Should().StartWith("Too many sign-in attempts.");
        result.ContentTypes.Should().Equal("application/problem+json");
        int.Parse(context.HttpContext.Response.Headers.RetryAfter.ToString()).Should().BeInRange(1, 60);
    }

    [Test]
    public async Task ThrottleByUsername_AnotherUsername_HasItsOwnAllowance()
    {
        await ThrottleByUsername(new LoginRequest(Username, Password));
        await ThrottleByUsername(new LoginRequest(Username, Password));

        var (_, ran) = await ThrottleByUsername(new LoginRequest(OtherUsername, Password));

        ran.Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ThrottleByUsername_NoUsername_LeavesItToValidation(string? username)
    {
        for (var i = 0; i < 3; i++)
        {
            var (_, ran) = await ThrottleByUsername(username is null ? null : new LoginRequest(username, Password));
            ran.Should().BeTrue();
        }
    }

    [Test]
    public void UsernameLimiter_UsesTheWindowFromSettings()
    {
        using var limiter = new LoginUsernameLimiter(Options.Create(new LoginThrottlingOptions { Window = TimeSpan.FromSeconds(30) }));

        limiter.Window.Should().Be(TimeSpan.FromSeconds(30));
    }
}
