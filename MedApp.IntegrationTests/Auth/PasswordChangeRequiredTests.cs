using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Notifications;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// The forced password change (<c>PasswordChangeRequired</c>), through the whole pipeline: an account an admin created
/// or reset can only change its own password (and ask who it is, read its settings, sign in and out) until it does.
/// </summary>
[TestFixture]
public sealed class PasswordChangeRequiredTests : IntegrationTest
{
    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static IEnumerable<TestCaseData> HeldEndpoints()
    {
        yield return Case("GET", "/api/patients/search");
        yield return Case("POST", "/api/patients");
        yield return Case("GET", "/api/procedures/patient-procedures/search");
        yield return Case("GET", "/api/notifications");
        yield return Case("PUT", "/api/auth/me/settings");
        yield return Case("GET", "/api/auth/users/search");
        yield return Case("GET", "/api/no-such-endpoint");
    }

    [TestCaseSource(nameof(HeldEndpoints))]
    public async Task ApiRequest_AdminCreatedUserBeforeChangingPassword_Returns403PasswordChangeRequired(string method, string url)
    {
        var forced = await CreateForcedUserAsync(AdminRole, UserRole);

        var response = await SendAsync(forced.Client, method, url);

        await ShouldBePasswordChangeRequiredAsync(response);
    }

    [Test]
    public async Task ApiRequest_AccountWithNoRoleYet_IsHeldByTheForcedChange()
    {
        // With no role it can't change its password (that needs the Staff policy), so it stays held.
        var forced = await CreateForcedUserAsync();

        await ShouldBePasswordChangeRequiredAsync(await forced.Client.GetAsync("/api/patients/search"));
    }

    [Test]
    public async Task ApiRequest_UserAfterAnAdminResetTheirPassword_Returns403PasswordChangeRequired()
    {
        var staff = await CreateStaffAsync(UserRole);
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var reset = await _admin.PutAsync($"/api/auth/users/{staff.Username}/reset-password", null);
        await reset.ShouldHaveStatusAsync(HttpStatusCode.OK);
        var generated = await reset.Content.ReadAsStringAsync();
        var client = await SignInAsync(staff.Username, generated);

        await ShouldBePasswordChangeRequiredAsync(await client.GetAsync("/api/patients/search"));
        // Their session from before the reset was ended by it.
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Hub_UserBeforeChangingPassword_RefusesTheConnection()
    {
        var forced = await CreateForcedUserAsync(UserRole);
        var login = await (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(forced.Username, forced.Password)))
            .ReadAsAsync<LoginResponse>();
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(MedAppApiFactory.BaseAddress, NotificationsHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Api.Server.CreateHandler();
                options.Headers["Cookie"] = $"{SessionAuthenticationDefaults.HostCookieName}={login.SessionId}";
            })
            .Build();

        var act = () => hub.StartAsync();

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await hub.DisposeAsync();
    }

    [Test]
    public async Task WhoAmI_UserBeforeChangingPassword_SaysTheyMustChangeIt()
    {
        var forced = await CreateForcedUserAsync(UserRole);

        var me = await (await forced.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.Should().BeEquivalentTo(new { IsAuthenticated = true, Name = forced.Username, MustChangePassword = true });
    }

    [Test]
    public async Task AllowedRequests_UserBeforeChangingPassword_Work()
    {
        var forced = await CreateForcedUserAsync(UserRole);

        await (await forced.Client.GetAsync("/api/auth/me/settings")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await forced.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(forced.Username, forced.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await forced.Client.PostAsync("/api/auth/logout", null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var me = await (await forced.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        me.IsAuthenticated.Should().BeFalse();
    }

    /// <summary>
    /// The pipeline test from the #59 review. The middleware lets a forced user change only their own password, which
    /// it checks against the <c>{username}</c> route value. That value only exists if routing has matched the endpoint
    /// before the middleware runs; if routing ran after it, the check would see no username and refuse every change,
    /// locking new users out for good. Through the real pipeline, their own change goes through.
    /// </summary>
    [Test]
    public async Task UpdatePassword_OwnAccountWhileForced_IsAllowedBecauseRoutingRunsBeforeTheMiddleware()
    {
        var forced = await CreateForcedUserAsync(UserRole);

        var response = await forced.Client.PutAsJsonAsync(
            $"/api/auth/users/{forced.Username}/update-password",
            new UpdateUserPasswordRequest(forced.Password, ChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task UpdatePassword_SomeoneElsesAccountWhileForced_Returns403PasswordChangeRequired()
    {
        var forced = await CreateForcedUserAsync(UserRole);
        var other = await CreateStaffAsync(UserRole);

        var response = await forced.Client.PutAsJsonAsync(
            $"/api/auth/users/{other.Username}/update-password",
            new UpdateUserPasswordRequest(other.Password, AnotherChosenPassword));

        await ShouldBePasswordChangeRequiredAsync(response);
        await SignInAsync(other.Username, other.Password);
    }

    [Test]
    public async Task UpdatePassword_OwnNameInAnotherCaseWhileForced_IsRefused()
    {
        // The route's name must be theirs exactly, as the SelfUserOnly policy compares it.
        var forced = await CreateForcedUserAsync(UserRole);

        var response = await forced.Client.PutAsJsonAsync(
            $"/api/auth/users/{forced.Username.ToUpperInvariant()}/update-password",
            new UpdateUserPasswordRequest(forced.Password, ChosenPassword));

        await ShouldBePasswordChangeRequiredAsync(response);
    }

    [Test]
    public async Task ApiRequests_AfterChangingPassword_WorkInTheSameSession()
    {
        var forced = await CreateForcedUserAsync(UserRole);
        await (await forced.Client.PutAsJsonAsync(
                $"/api/auth/users/{forced.Username}/update-password",
                new UpdateUserPasswordRequest(forced.Password, ChosenPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await forced.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await forced.Client.GetAsync("/api/notifications")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var me = await (await forced.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        me.MustChangePassword.Should().BeFalse();
        // And from a new sign-in with the new password.
        var again = await SignInAsync(forced.Username, ChosenPassword);
        await (await again.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task FirstAdmin_MadeByTheDeploy_IsNotForcedToChangeTheirPassword()
    {
        var me = await (await _admin.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();

        me.MustChangePassword.Should().BeFalse();
        await (await _admin.GetAsync("/api/auth/users/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignedOutRequest_IsNotHeld_ButStillNeedsSigningIn()
    {
        await (await NewClient().GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    /// <summary>An account an admin made, with the given roles, signed in with its generated password (not changed).</summary>
    private async Task<StaffAccount> CreateForcedUserAsync(params string[] roles)
    {
        var username = UniqueName("forced");
        var created = await (await _admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username)))
            .ReadAsAsync<CreateUserResponse>();
        await (await _admin.PutAsJsonAsync($"/api/auth/users/{username}/roles", new SetUserRolesRequest(roles)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var userId = await GetUserIdAsync(_admin, username);
        return new StaffAccount(userId, username, created.Password, await SignInAsync(username, created.Password));
    }

    private static async Task ShouldBePasswordChangeRequiredAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden);
        problem.Title.Should().Be(PasswordChangeRequired.Title);
        problem.Extensions.Should().ContainKey("code");
        ((JsonElement)problem.Extensions["code"]!).GetString().Should().Be(PasswordChangeRequired.Code);
    }

    private static TestCaseData Case(string method, string url) =>
        new TestCaseData(method, url).SetArgDisplayNames(method, url);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }
}
