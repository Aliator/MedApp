using FluentAssertions;
using MedApp.API.Common.RateLimiting;
using MedApp.Contracts.Authentication.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedApp.UnitTests.Api.RateLimiting;

[TestFixture]
public sealed class ResetRequestThrottlingTests
{
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LoginThrottling:ResetRequestsPerIdentifierPermitLimit"] = "2",
                ["LoginThrottling:ResetRequestWindow"] = "00:15:00",
            })
            .Build();
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Options.Create(new IdentityOptions()))
            .AddSingleton<IConfiguration>(configuration)
            .AddLoginThrottling()
            .BuildServiceProvider();
    }

    [TearDown]
    public void TearDown() => _services.Dispose();

    private async Task<(ActionExecutingContext Context, bool Ran)> Throttle(ForgotPasswordRequest? request)
    {
        var http = new DefaultHttpContext { RequestServices = _services };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var arguments = new Dictionary<string, object?>();
        if (request is not null) arguments["request"] = request;
        var context = new ActionExecutingContext(actionContext, [], arguments, controller: new object());
        var ran = false;

        await new ThrottleResetRequestsAttribute().OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        });
        return (context, ran);
    }

    [Test]
    public async Task OverTheLimit_IsRefusedWithRetryAfterWhateverTheCase()
    {
        (await Throttle(new ForgotPasswordRequest("someone@example.test"))).Ran.Should().BeTrue();
        (await Throttle(new ForgotPasswordRequest("SOMEONE@example.test "))).Ran.Should().BeTrue();

        var (context, ran) = await Throttle(new ForgotPasswordRequest("someone@EXAMPLE.test"));

        ran.Should().BeFalse();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        result.Value.Should().BeOfType<ProblemDetails>().Which.Title.Should().Contain("Try again in").And.Contain("minutes");
        context.HttpContext.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
    }

    [Test]
    public async Task DifferentIdentifiers_AreCountedApart()
    {
        await Throttle(new ForgotPasswordRequest("one.user"));
        await Throttle(new ForgotPasswordRequest("one.user"));

        (await Throttle(new ForgotPasswordRequest("another.user"))).Ran.Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("﻿not something any account could have \u0000")]
    public async Task NothingCountable_IsLeftToValidationAndTheLookup(string? identifier)
    {
        var request = identifier is null ? null : new ForgotPasswordRequest(identifier);

        for (var i = 0; i < 5; i++)
            (await Throttle(request)).Ran.Should().BeTrue();
    }

    [TestCase(1, "1 second")]
    [TestCase(30, "30 seconds")]
    [TestCase(60, "1 minute")]
    [TestCase(61, "2 minutes")]
    [TestCase(900, "15 minutes")]
    public void TooManyRequests_SaysHowLongToWait(int seconds, string wait) =>
        ThrottleResetRequestsAttribute.TooManyRequests(seconds).Title.Should().EndWith($"Try again in {wait}.");

    [Test]
    public void Options_Defaults_AreThreeEveryFifteenMinutes()
    {
        var defaults = new LoginThrottlingOptions();

        defaults.ResetRequestsPerIdentifierPermitLimit.Should().Be(3);
        defaults.ResetRequestWindow.Should().Be(TimeSpan.FromMinutes(15));
    }
}
