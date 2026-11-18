using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.Domain.Authentication;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.AccountSecurity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Two-factor codes by email through the whole pipeline: turning them on (proving the mailbox), signing in with an
/// emailed code, wrong and expired codes, sending again and its limits, turning them off, the address they go to, and
/// the admin requirement, which needs the app. Codes are read from the emails the fake sender keeps.
/// </summary>
[TestFixture]
public sealed class EmailCodeTests : IntegrationTest
{
    private static Task<HttpResponseMessage> SecondStepAsync(HttpClient client, string challenge, string? code = null, string? recoveryCode = null) =>
        client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge, code, recoveryCode));

    private static Task<HttpResponseMessage> ResendAsync(HttpClient client, string challenge) =>
        client.PostAsJsonAsync("/api/auth/login/two-factor/resend", new ResendSignInCodeRequest(challenge));

    private static async Task<TwoFactorStatusResponse> StatusAsync(HttpClient client) =>
        await (await client.GetAsync("/api/auth/me/two-factor")).ReadAsAsync<TwoFactorStatusResponse>();

    private static string Wrong(string right) => right == "000000" ? "111111" : "000000";

    [Test]
    public async Task TurningOn_NeedsAConfirmedAddress_ThenThePassword_ThenTheEmailedCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var elsewhere = await SignInAsync(staff.Username, staff.Password);
        var address = NewAddress();

        // No confirmed address yet: unavailable, and refused.
        (await StatusAsync(staff.Client)).Should().Be(new TwoFactorStatusResponse(false, 0, false, "None", false, "NoConfirmedAddress"));
        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email", new EmailCodesSetupRequest(staff.Password)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        (await StatusAsync(staff.Client)).CanUseEmailCodes.Should().BeTrue();
        var wrongPassword = await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email", new EmailCodesSetupRequest("not-the-password"));
        await wrongPassword.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        CodeEmailsTo(address).Should().Be(0);

        var sent = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email", new EmailCodesSetupRequest(staff.Password)))
            .ReadAsAsync<EmailCodeSentResponse>(HttpStatusCode.Accepted);
        sent.SentTo.Should().Be(EmailAddressMask.Mask(address)).And.NotBe(address);
        sent.ResendInSeconds.Should().Be(60);
        sent.ResendsLeft.Should().Be(EmailCode.MaxResends);
        var code = EmailedCodeTo(address);

        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/enable", new EmailCodeRequest(Wrong(code))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var codes = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/enable", new EmailCodeRequest(code)))
            .ReadAsAsync<RecoveryCodesResponse>();

        codes.RecoveryCodes.Should().HaveCount(10);
        (await StatusAsync(staff.Client)).Should().Be(new TwoFactorStatusResponse(true, 10, false, "Email", true, null));
        await ShouldBeSignedInAsync(staff.Client);
        await ShouldBeSignedInAsync(elsewhere, false);
        Api.Emails.To(address).Should().Contain(m => m.Subject == "Sign-in codes by email are on for your MedApp account");
        // The code worked once.
        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/enable", new EmailCodeRequest(code)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task SignIn_EmailsACode_ThatSignsInOnce()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();

        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);

        challenge.Method.Should().Be("Email");
        challenge.EmailCode!.SentTo.Should().Be(EmailAddressMask.Mask(address));
        // The test clock runs on real time, so the moment read here is a little after the request's.
        challenge.ExpiresAtUtc.Should().BeCloseTo(Api.Clock.GetUtcNow().UtcDateTime.Add(SignInChallenge.EmailLifetime), TimeSpan.FromSeconds(5));
        var email = Api.Emails.To(address).Last(m => m.Subject == EmailTemplates.TwoFactorCodeSubject);
        email.TextBody.Should().NotContain(staff.Username).And.Contain("10 minutes");
        var code = FakeEmailSender.CodeIn(email);
        await ShouldBeSignedInAsync(client, false);

        await (await SecondStepAsync(client, challenge.Challenge, code)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await ShouldBeSignedInAsync(client);

        // Single use: the challenge and its code are gone.
        var again = await SecondStepAsync(NewClient(), challenge.Challenge, code);
        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(again)).Should().Be(SignInExpiredException.Code);
        (await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().EmailCodes.CountAsync(c => c.UserId == staff.UserId)))
            .Should().Be(0);
    }

    [Test]
    public async Task SignIn_StoresOnlyAHashOfTheCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);

        await PasswordStepAsync(NewClient(), staff.Username, staff.Password);

        var code = EmailedCodeTo(address);
        var stored = await RunInScopeAsync(services =>
            services.GetRequiredService<MedAppDbContext>().EmailCodes.AsNoTracking().SingleAsync(c => c.UserId == staff.UserId));
        stored.CodeHash.Should().HaveLength(64).And.NotContain(code);
        stored.Purpose.Should().Be(EmailCodePurpose.SignIn);
    }

    [Test]
    public async Task SignIn_WrongCode_CountsTowardsTheLockout_AndARecoveryCodeStillWorks()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        var recoveryCodes = await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var code = EmailedCodeTo(address);

        var wrong = await SecondStepAsync(client, challenge.Challenge, Wrong(code));

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(wrong)).Should().BeNull();
        (await RunInScopeAsync(async services =>
                (await services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<MedApp.Infrastructure.Common.Identity.ApplicationUser>>()
                    .FindByIdAsync(staff.UserId.ToString()))!.AccessFailedCount))
            .Should().Be(1);

        await (await SecondStepAsync(client, challenge.Challenge, recoveryCode: recoveryCodes[0])).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignIn_FiveWrongCodes_LockTheAccountAndTellTheHolder()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var wrong = Wrong(EmailedCodeTo(address));

        for (var i = 0; i < 4; i++)
            (await SecondStepAsync(client, challenge.Challenge, wrong)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var locked = await SecondStepAsync(client, challenge.Challenge, wrong);

        locked.StatusCode.Should().Be((HttpStatusCode)423);
        Api.Emails.To(address).Should().ContainSingle(m => m.Subject == "Wrong sign-in codes locked your MedApp account");
    }

    [Test]
    public async Task SignIn_AfterTenMinutes_TheCodeAndTheSignInHaveRunOut()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var code = EmailedCodeTo(address);

        Api.Clock.Advance(EmailCode.Lifetime);
        var late = await SecondStepAsync(client, challenge.Challenge, code);

        late.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(late)).Should().Be(SignInExpiredException.Code);
        await ShouldBeSignedInAsync(client, false);
    }

    [Test]
    public async Task SignIn_JustInsideTenMinutes_TheCodeStillWorks()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var code = EmailedCodeTo(address);

        // Longer than an authenticator sign-in's five minutes, as an email can take a while.
        Api.Clock.Advance(EmailCode.Lifetime - TimeSpan.FromSeconds(5));

        await (await SecondStepAsync(client, challenge.Challenge, code)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Resend_OnceAMinuteThreeTimes_EachNewCodeReplacingTheLast()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var first = EmailedCodeTo(address);
        var sentBefore = CodeEmailsTo(address);

        // Too soon.
        var tooSoon = await ResendAsync(client, challenge.Challenge);
        tooSoon.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        tooSoon.Headers.RetryAfter.Should().NotBeNull();

        string latest = first;
        for (var i = 1; i <= EmailCode.MaxResends; i++)
        {
            Api.Clock.Advance(EmailCode.ResendInterval);
            var sent = await (await ResendAsync(client, challenge.Challenge)).ReadAsAsync<EmailCodeSentResponse>();
            sent.ResendsLeft.Should().Be(EmailCode.MaxResends - i);
            latest = EmailedCodeTo(address);
        }

        Api.Clock.Advance(EmailCode.ResendInterval);
        (await ResendAsync(client, challenge.Challenge)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        CodeEmailsTo(address).Should().Be(sentBefore + EmailCode.MaxResends);

        // Only the newest code signs in (unless an earlier one happens to be the same digits).
        if (first != latest)
            (await SecondStepAsync(client, challenge.Challenge, first)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await (await SecondStepAsync(client, challenge.Challenge, latest)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Resend_ForAnAuthenticatorAccountOrAnUnknownChallenge_IsRefused()
    {
        var staff = await CreateStaffAsync(UserRole);
        await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);

        challenge.Method.Should().Be("Authenticator");
        challenge.EmailCode.Should().BeNull();
        (await ResendAsync(client, challenge.Challenge)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var unknown = await ResendAsync(client, "not-a-challenge");
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(unknown)).Should().Be(SignInExpiredException.Code);
    }

    [Test]
    public async Task TurningOff_NeedsThePasswordAndAnEmailedCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);

        // The password first, as turning codes on asks.
        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/code", new SendEmailCodeRequest("not-the-password")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var sent = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/code", new SendEmailCodeRequest(staff.Password)))
            .ReadAsAsync<EmailCodeSentResponse>(HttpStatusCode.Accepted);
        sent.SentTo.Should().Be(EmailAddressMask.Mask(address));
        var code = EmailedCodeTo(address);
        Api.Emails.To(address).Last(m => m.Subject == EmailTemplates.TwoFactorCodeSubject).TextBody.Should().Contain("to confirm a change");

        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, Wrong(code))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, code)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await StatusAsync(staff.Client)).Method.Should().Be("None");
        await (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, staff.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task NewRecoveryCodes_WithAnEmailedCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/code", new SendEmailCodeRequest(staff.Password)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);

        var codes = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/recovery-codes", new TwoFactorCodeRequest(staff.Password, EmailedCodeTo(address))))
            .ReadAsAsync<RecoveryCodesResponse>();

        codes.RecoveryCodes.Should().HaveCount(10);
    }

    [Test]
    public async Task TurningOff_AfterFiveWrongCodes_ANewCodeWorks()
    {
        // The review of #118, finding 4: the resend used to send a code that could never work.
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var wrong = Wrong(await ConfirmCodeAsync(staff.Client, staff.Password, address));
        for (var i = 0; i < 5; i++)
            await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, wrong));

        // The five wrong codes locked the account (as five wrong app codes would); once that's over, a new code works.
        Api.Clock.Advance(TimeSpan.FromMinutes(6));
        var fresh = await ConfirmCodeAsync(staff.Client, staff.Password, address);

        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, fresh)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task TurningOn_TwoFirstSendsAtOnce_OnlyOneCodeStands()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        var start = new TaskCompletionSource();

        var sends = Enumerable.Range(0, 2).Select(async _ =>
        {
            await start.Task;
            return await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email", new EmailCodesSetupRequest(staff.Password));
        }).ToList();
        start.SetResult();
        var responses = await Task.WhenAll(sends);

        // However they interleave, the code in the last email is the one that turns codes on.
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.Accepted);
        (await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().EmailCodes
                .CountAsync(c => c.UserId == staff.UserId && c.Purpose == EmailCodePurpose.TurnOn)))
            .Should().Be(1);
        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/enable", new EmailCodeRequest(EmailedCodeTo(address))))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SendingAConfirmCode_WithoutEmailCodes_IsRefused()
    {
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/email/code", new SendEmailCodeRequest(staff.Password)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task TheAddress_CantBeRemovedWhileUsedForCodes_AndAChangeOnlyCountsOnceConfirmed()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var admin = await SignInAsAdminAsync();

        (await staff.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/auth/me/email")
            { Content = JsonContent.Create(new RemoveEmailRequest(staff.Password)) })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.DeleteAsync($"/api/auth/users/{staff.Username}/email")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Moving the address needs the current factor too: the password alone isn't enough.
        var newAddress = NewAddress();
        (await staff.Client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(newAddress, staff.Password)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        CodeEmailsTo(newAddress).Should().Be(0);
        var confirm = await ConfirmCodeAsync(staff.Client, staff.Password, address);
        await (await staff.Client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(newAddress, staff.Password, confirm)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);

        // A new address waiting to be confirmed gets no codes; the confirmed one still does.
        await PasswordStepAsync(NewClient(), staff.Username, staff.Password);
        CodeEmailsTo(newAddress).Should().Be(0);
        EmailedCodeTo(address).Should().NotBeNullOrEmpty();

        // Once it's confirmed, codes go there.
        await ConfirmFromEmailAsync(staff.Client, newAddress);
        await SignInWithEmailCodeAsync(staff.Username, staff.Password, newAddress);
    }

    [Test]
    public async Task RequirementForAdmins_NeedsTheApp_SoAnAdminWithEmailCodesIsHeldToSwitch()
    {
        var admin = await SignInAsAdminAsync();
        var otherAdmin = await CreateStaffAsync(AdminRole, UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(otherAdmin.Client, otherAdmin.Password, address);

        // Email codes aren't enough to turn the requirement on, either.
        (await otherAdmin.Client.PutAsJsonAsync("/api/auth/security-settings", new SaveSecuritySettingsRequest(true)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        await TurnOnTwoFactorAsync(admin, AdminPassword);
        var settings = await (await admin.PutAsJsonAsync("/api/auth/security-settings", new SaveSecuritySettingsRequest(true)))
            .ReadAsAsync<SecuritySettingsResponse>();
        settings.AdminsWithoutTwoFactorUsernames.Should().Contain(otherAdmin.Username);

        // Held, even signing in afresh with an emailed code.
        var client = await SignInWithEmailCodeAsync(otherAdmin.Username, otherAdmin.Password, address);
        (await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>()).MustSetUpTwoFactor.Should().BeTrue();
        var held = await client.GetAsync("/api/patients/search");
        held.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(held)).Should().Be(TwoFactorSetupRequired.Code);
        var status = await StatusAsync(client);
        status.Method.Should().Be("Email");
        status.Required.Should().BeTrue();
        (await client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(otherAdmin.Password, "000000")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Switching needs the current factor, not only the password and the new app's code.
        var setup = await (await client.PostAsync("/api/auth/me/two-factor/authenticator", null)).ReadAsAsync<AuthenticatorSetupResponse>();
        var withoutCurrent = await client.PostAsJsonAsync("/api/auth/me/two-factor/enable",
            new TwoFactorCodeRequest(otherAdmin.Password, NextCode(setup.SharedKey)));
        await withoutCurrent.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await withoutCurrent.Content.ReadAsStringAsync()).Should().Contain("CurrentCode");
        (await StatusAsync(client)).Method.Should().Be("Email");

        // Setting up the app replaces the email codes (two-factor never goes off) and frees them; the hold lets them ask
        // for the emailed code that proves the current factor, and the other admins are told.
        Api.Emails.Clear();
        var adminAddress = NewAddress();
        await ConfirmAddressAsync(admin, AdminPassword, adminAddress);
        var (key, _) = await SwitchToAppAsync(client, otherAdmin.Password, address);
        Api.Emails.To(adminAddress).Should().Contain(m => m.Subject == "An administrator moved their sign-in codes to an authenticator app");
        Api.Emails.To(address).Should().Contain(m => m.Subject == "Your MedApp sign-in codes moved to an authenticator app");
        await (await client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await StatusAsync(client)).Method.Should().Be("Authenticator");
        await SignInWithTwoFactorAsync(otherAdmin.Username, otherAdmin.Password, key);
    }

    [Test]
    public async Task ActivityLog_RecordsEmailCodesOnTheEmailStepAndOff_NeverTheCodeOrTheAddress()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await TurnOnEmailCodesAsync(staff.Client, staff.Password, address);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        var code = EmailedCodeTo(address);
        await SecondStepAsync(client, challenge.Challenge, Wrong(code));
        await (await SecondStepAsync(client, challenge.Challenge, code)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var confirm = await ConfirmCodeAsync(client, staff.Password, address);
        await (await client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, confirm)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var admin = await SignInAsAdminAsync();
        var page = await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={staff.UserId}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        var actions = page.Items.Select(e => e.Action).ToList();

        actions.Should().Contain("EmailCodesEnabled").And.Contain("EmailCodesDisabled").And.Contain("TwoFactorChallenged");
        page.Items.Should().Contain(e => e.Action == "SignedIn" && e.Details.Contains("\"secondStep\":\"email\""));
        page.Items.Should().Contain(e => e.Action == "SignInFailed" && e.Details.Contains("InvalidCode"));
        page.Items.Should().OnlyContain(e => !e.Details.Contains(code) && !e.Details.Contains(confirm) && !e.Details.Contains(address));
    }
}
