using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Sign-in throttling through the real pipeline. Every other test runs with the limits out of reach
/// (<see cref="MedAppApiFactory"/>); these put them back, on an API of their own (so their counts start at nothing).
/// </summary>
[TestFixture]
public sealed class LoginThrottlingTests : IntegrationTest
{
    /// <summary>An API with these limits, and a client for it.</summary>
    private static (WebApplicationFactory<Program> Api, HttpClient Client) ApiWithLimits(int perClient, int perUsername)
    {
        var api = Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("LoginThrottling:PerClientPermitLimit", perClient.ToString());
            builder.UseSetting("LoginThrottling:PerUsernamePermitLimit", perUsername.ToString());
        });
        var client = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = MedAppApiFactory.BaseAddress,
            AllowAutoRedirect = false,
        });
        return (api, client);
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

    [Test]
    public async Task Login_SixthAttemptAtOneUsernameInAMinute_Returns429WithRetryAfter()
    {
        // The real limits.
        var (api, client) = ApiWithLimits(perClient: 30, perUsername: 5);
        await using var _ = api;
        var username = UniqueName("nobody");

        for (var i = 0; i < 5; i++)
            await (await SignIn(client, username, WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        var refused = await SignIn(client, username, WrongPassword);

        var problem = await refused.ReadAsAsync<ProblemDetails>(HttpStatusCode.TooManyRequests);
        problem.Title.Should().StartWith("Too many sign-in attempts. Try again in");
        refused.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);
        // Another name still gets its own allowance.
        await (await SignIn(client, AdminUsername, AdminPassword)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_OverTheClientLimit_Returns429EvenWithTheRightPassword()
    {
        var (api, client) = ApiWithLimits(perClient: 3, perUsername: 100);
        await using var _ = api;

        for (var i = 0; i < 3; i++)
            await (await SignIn(client, UniqueName("nobody"), WrongPassword)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);

        var refused = await SignIn(client, AdminUsername, AdminPassword);

        await refused.ShouldHaveStatusAsync(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().NotBeNull();
        refused.Headers.Contains("Set-Cookie").Should().BeFalse();
    }
}
