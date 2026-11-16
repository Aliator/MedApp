using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Infrastructure.Common.Identity;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.AccountSecurity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Codes guessed all at once (the review of #86, finding 1): requests that arrive together must not get more checks than
/// requests sent one after another. Each sends 30 wrong codes at the same moment, against the real database.
/// </summary>
[TestFixture]
public sealed class ParallelCodeTests : IntegrationTest
{
    private const int AtOnce = 30;
    private const string WrongCode = "000000";

    private static async Task<HttpResponseMessage[]> AllAtOnceAsync(Func<Task<HttpResponseMessage>> send)
    {
        var start = new TaskCompletionSource();
        var requests = Enumerable.Range(0, AtOnce).Select(async _ =>
        {
            await start.Task;
            return await send();
        }).ToList();
        start.SetResult();
        return await Task.WhenAll(requests);
    }

    private static Task<ApplicationUser> AccountAsync(Guid userId) =>
        RunInScopeAsync(async services =>
            (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString()))!);

    [Test]
    public async Task OneChallenge_ThirtyWrongCodesAtOnce_AtMostFiveAreCheckedAndTheLockoutLandsOnce()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        Api.Emails.Clear();

        var responses = await AllAtOnceAsync(() =>
            client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge.Challenge, WrongCode, null)));

        // No request fails on a lost update: each is refused for a reason.
        responses.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized || s == (HttpStatusCode)423);
        var codes = await Task.WhenAll(responses.Select(async r => (r.StatusCode, Code: await ProblemCodeAsync(r))));
        // The challenge's five attempts are reserved before any code is checked: four are wrong codes, the fifth locks.
        codes.Count(c => c.StatusCode == HttpStatusCode.Unauthorized && c.Code is null).Should().Be(4);
        codes.Count(c => c.StatusCode == HttpStatusCode.Unauthorized && c.Code == SignInExpiredException.Code
                         || c.StatusCode == (HttpStatusCode)423).Should().Be(AtOnce - 4);

        var account = await AccountAsync(staff.UserId);
        account.LockoutEnd.Should().BeAfter(Api.Clock.GetUtcNow());
        account.TwoFactorLockouts.Should().Be(1);
        Api.Emails.To(address).Should().ContainSingle(m => m.Subject == "Wrong sign-in codes locked your MedApp account");
        await ShouldBeSignedInAsync(client, false);
    }

    [Test]
    public async Task OneChallenge_ThirtyRightCodesAtOnce_SignInOnce()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var challenge = await PasswordStepAsync(NewClient(), staff.Username, staff.Password);
        var code = NextCode(key);

        var responses = await AllAtOnceAsync(() =>
            NewClient().PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge.Challenge, code, null)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.OK || s == HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task NewRecoveryCodes_ThirtyWrongCodesAtOnce_AreLimitedPerAccount()
    {
        // The real per-account limit (10 in 15 minutes); the lockout out of reach, so the limit alone decides.
        using var api = Api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("LoginThrottling:TwoFactorCodesPerAccountPermitLimit", "10");
            builder.UseSetting("Identity:MaxFailedAccessAttempts", "1000");
        });
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = MedAppApiFactory.BaseAddress, AllowAutoRedirect = false });
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        await (await client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge.Challenge, NextCode(key), null)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var responses = await AllAtOnceAsync(() =>
            client.PostAsJsonAsync("/api/auth/me/two-factor/recovery-codes", new TwoFactorCodeRequest(staff.Password, WrongCode)));

        // The sign-in used one of the ten.
        responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest).Should().Be(9);
        responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(AtOnce - 9);
        responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Should().OnlyContain(r => r.Headers.RetryAfter != null);
    }

    [Test]
    public async Task NewRecoveryCodes_WithoutThePassword_AreRefusedBeforeAnyCodeIsChecked()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);

        var refused = await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/recovery-codes", new TwoFactorCodeRequest("not-the-password", NextCode(key)));

        await refused.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Password");
        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/recovery-codes", new TwoFactorCodeRequest(staff.Password, NextCode(key))))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task TurningTwoFactorOn_WithoutThePassword_IsRefused()
    {
        var staff = await CreateStaffAsync(UserRole);
        var setup = await (await staff.Client.PostAsync("/api/auth/me/two-factor/authenticator", null))
            .ReadAsAsync<MedApp.Contracts.Authentication.Responses.AuthenticatorSetupResponse>();

        var refused = await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/enable", new TwoFactorCodeRequest("not-the-password", NextCode(setup.SharedKey)));

        await refused.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await (await staff.Client.GetAsync("/api/auth/me/two-factor"))
            .ReadAsAsync<MedApp.Contracts.Authentication.Responses.TwoFactorStatusResponse>()).Enabled.Should().BeFalse();
    }

    [Test]
    public async Task CodeLockouts_GrowEachTime_AndACompleteSignInStartsThemAgain()
    {
        var staff = await CreateStaffAsync(UserRole);
        var start = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

        Task<TimeSpan?[]> FiveWrongCodesAsync(DateTimeOffset now) =>
            RunInScopeAsync(async services =>
            {
                var lockouts = services.GetRequiredService<MedApp.Application.Authentication.TwoFactor.Repositories.IAccountLockoutRepository>();
                var results = new List<TimeSpan?>();
                for (var i = 0; i < 5; i++)
                    results.Add(await lockouts.RecordFailedCodeAsync(staff.UserId, 5, TimeSpan.FromMinutes(15), now, CancellationToken.None));
                return results.ToArray();
            });

        (await FiveWrongCodesAsync(start)).Should().Equal(null, null, null, null, TimeSpan.FromMinutes(15));
        (await FiveWrongCodesAsync(start.AddHours(1))).Should().Equal(null, null, null, null, TimeSpan.FromMinutes(30));
        var account = await AccountAsync(staff.UserId);
        account.LockoutEnd.Should().Be(start.AddHours(1).AddMinutes(30));
        account.TwoFactorLockouts.Should().Be(2);
        account.AccessFailedCount.Should().Be(0);

        // Wrong passwords lock for the set time, and don't make code lockouts grow.
        var passwordLock = await RunInScopeAsync(async services =>
        {
            var lockouts = services.GetRequiredService<MedApp.Application.Authentication.TwoFactor.Repositories.IAccountLockoutRepository>();
            TimeSpan? last = null;
            for (var i = 0; i < 5; i++)
                last = await lockouts.RecordFailedPasswordAsync(staff.UserId, 5, TimeSpan.FromMinutes(5), start.AddHours(2), CancellationToken.None);
            return last;
        });
        passwordLock.Should().Be(TimeSpan.FromMinutes(5));
        (await AccountAsync(staff.UserId)).TwoFactorLockouts.Should().Be(2);

        await RunInScopeAsync(services => services
            .GetRequiredService<MedApp.Application.Authentication.TwoFactor.Repositories.IAccountLockoutRepository>()
            .ResetAsync(staff.UserId, CancellationToken.None));
        (await AccountAsync(staff.UserId)).TwoFactorLockouts.Should().Be(0);
    }
}
