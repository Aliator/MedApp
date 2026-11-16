using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.AccountSecurity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// Two-factor authentication through the whole pipeline: setting it up, signing in in two steps (codes, recovery codes,
/// the challenge's expiry and single use, the lockout), turning it off, an admin's reset, and the requirement for
/// admins with its hold. Codes are made as an authenticator app makes them, from the key the API shows.
/// </summary>
[TestFixture]
public sealed class TwoFactorTests : IntegrationTest
{
    private static Task<HttpResponseMessage> SecondStepAsync(HttpClient client, string challenge, string? code = null, string? recoveryCode = null) =>
        client.PostAsJsonAsync("/api/auth/login/two-factor", new LoginTwoFactorRequest(challenge, code, recoveryCode));

    [Test]
    public async Task AccountWithoutTwoFactor_SignsInAsBefore()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUsername, AdminPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<LoginResponse>())!.SessionId.Should().NotBeEmpty();
    }

    [Test]
    public async Task SetUp_ShowsAKeyAndQrCode_AndTurnsOnOnlyWithARightCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var setup = await (await staff.Client.PostAsync("/api/auth/me/two-factor/authenticator", null)).ReadAsAsync<AuthenticatorSetupResponse>();

        setup.AuthenticatorUri.Should().StartWith($"otpauth://totp/MedApp%3A{staff.Username}?secret=");
        setup.QrCodePath.Should().StartWith("M");
        setup.QrCodeSize.Should().BeGreaterThan(20);

        var wrong = await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/enable", new TwoFactorCodeRequest(staff.Password, "000000"));
        await wrong.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await (await staff.Client.GetAsync("/api/auth/me/two-factor")).ReadAsAsync<TwoFactorStatusResponse>()).Enabled.Should().BeFalse();

        var codes = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/enable", new TwoFactorCodeRequest(staff.Password, NextCode(setup.SharedKey))))
            .ReadAsAsync<RecoveryCodesResponse>();
        codes.RecoveryCodes.Should().HaveCount(10).And.OnlyHaveUniqueItems();

        var status = await (await staff.Client.GetAsync("/api/auth/me/two-factor")).ReadAsAsync<TwoFactorStatusResponse>();
        status.Should().Be(new TwoFactorStatusResponse(true, 10, false));
        // Setting up another app while it's on is refused, so the working one can't be replaced silently.
        (await staff.Client.PostAsync("/api/auth/me/two-factor/authenticator", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task TurningItOn_EndsTheOtherSessionsAndKeepsThisOne()
    {
        var staff = await CreateStaffAsync(UserRole);
        var elsewhere = await SignInAsync(staff.Username, staff.Password);

        await TurnOnTwoFactorAsync(staff.Client, staff.Password);

        await ShouldBeSignedInAsync(staff.Client);
        await ShouldBeSignedInAsync(elsewhere, false);
    }

    [Test]
    public async Task SignIn_PasswordGivesAChallengeWithNoSession_ThenTheCodeGivesTheSession()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();

        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);

        challenge.Challenge.Should().NotBeNullOrEmpty();
        await ShouldBeSignedInAsync(client, false);
        (await client.GetAsync("/api/patients/search")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await (await SecondStepAsync(client, challenge.Challenge, NextCode(key))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignIn_WrongCode_Is401AndTheSameChallengeCanTryAgain()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);

        var wrong = await SecondStepAsync(client, challenge.Challenge, "000000");

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(wrong)).Should().BeNull();
        await (await SecondStepAsync(client, challenge.Challenge, NextCode(key))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task SignIn_AChallengeWorksOnce()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        await (await SecondStepAsync(client, challenge.Challenge, NextCode(key))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var again = await SecondStepAsync(NewClient(), challenge.Challenge, NextCode(key));

        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(again)).Should().Be(SignInExpiredException.Code);
    }

    [Test]
    public async Task SignIn_ChallengeExpiresAfterFiveMinutes()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);

        Api.Clock.Advance(TimeSpan.FromMinutes(5));
        var late = await SecondStepAsync(client, challenge.Challenge, CodeNow(key));

        late.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ProblemCodeAsync(late)).Should().Be(SignInExpiredException.Code);
        await ShouldBeSignedInAsync(client, false);
    }

    [Test]
    public async Task SignIn_TheSameCodeTwice_IsRefusedTheSecondTime()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var code = NextCode(key);
        var first = NewClient();
        await (await SecondStepAsync(first, (await PasswordStepAsync(first, staff.Username, staff.Password)).Challenge, code))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var second = NewClient();
        var replayed = await SecondStepAsync(second, (await PasswordStepAsync(second, staff.Username, staff.Password)).Challenge, code);

        replayed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SignIn_WrongCodesCountTowardsTheLockout_EvenAcrossNewChallenges()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);

        // Entering the right password again doesn't clear the count: only a complete sign-in does.
        HttpResponseMessage last = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var client = NewClient();
            var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
            last = await SecondStepAsync(client, challenge.Challenge, "000000");
        }

        last.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, staff.Password)))
            .StatusCode.Should().Be(HttpStatusCode.Locked);
        key.Should().NotBeNull();
    }

    [Test]
    public async Task SignIn_ARecoveryCodeWorksOnce_AndTheOwnerIsTold()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        var (_, codes) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();

        await (await SecondStepAsync(client, (await PasswordStepAsync(client, staff.Username, staff.Password)).Challenge, recoveryCode: codes[0].ToLowerInvariant()))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        Api.Emails.To(address).Should().Contain(m => m.Subject == "A recovery code was used to sign in to MedApp" && m.TextBody.Contains("you have 9 left"));
        var status = await (await client.GetAsync("/api/auth/me/two-factor")).ReadAsAsync<TwoFactorStatusResponse>();
        status.RecoveryCodesLeft.Should().Be(9);

        var other = NewClient();
        (await SecondStepAsync(other, (await PasswordStepAsync(other, staff.Username, staff.Password)).Challenge, recoveryCode: codes[0]))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ForcedPasswordChange_ComesAfterTheCode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var admin = await SignInAsAdminAsync();
        var reset = await (await admin.PutAsync($"/api/auth/users/{staff.Username}/reset-password", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        // A bare string comes back as text/plain, not JSON.
        var generated = await reset.Content.ReadAsStringAsync();

        var client = await SignInWithTwoFactorAsync(staff.Username, generated, key);

        var whoAmI = await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        whoAmI.MustChangePassword.Should().BeTrue();
        (await ProblemCodeAsync(await client.GetAsync("/api/patients/search"))).Should().Be(PasswordChangeRequired.Code);
    }

    [Test]
    public async Task TurningItOff_NeedsThePasswordAndACode_AndSignInIsPasswordOnlyAgain()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        var (key, _) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);

        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(WrongPassword, NextCode(key))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, "000000")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(staff.Password, NextCode(key))))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await SignInAsync(staff.Username, staff.Password);
        Api.Emails.To(address).Select(m => m.Subject).Should().Contain(
            ["Two-factor authentication is on for your MedApp account", "Two-factor authentication is off for your MedApp account"]);
    }

    [Test]
    public async Task NewRecoveryCodes_ReplaceTheOldOnes()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, old) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);

        var fresh = await (await staff.Client.PostAsJsonAsync("/api/auth/me/two-factor/recovery-codes", new TwoFactorCodeRequest(staff.Password, NextCode(key))))
            .ReadAsAsync<RecoveryCodesResponse>();

        fresh.RecoveryCodes.Should().HaveCount(10).And.NotIntersectWith(old);
        var client = NewClient();
        (await SecondStepAsync(client, (await PasswordStepAsync(client, staff.Username, staff.Password)).Challenge, recoveryCode: old[0]))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var again = NewClient();
        await (await SecondStepAsync(again, (await PasswordStepAsync(again, staff.Username, staff.Password)).Challenge, recoveryCode: fresh.RecoveryCodes[0]))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task AdminReset_TurnsItOffEndsEverySessionAndTheUserSignsInWithThePasswordAlone()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, staff.Password, address);
        await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var admin = await SignInAsAdminAsync();
        var listed = await (await admin.GetAsync($"/api/auth/users/{staff.Username}")).ReadAsAsync<UserResponse>();
        listed.TwoFactorEnabled.Should().BeTrue();
        listed.Email.Should().Be(address);

        await (await admin.PostAsync($"/api/auth/users/{staff.Username}/reset-two-factor", null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await ShouldBeSignedInAsync(staff.Client, false);
        await SignInAsync(staff.Username, staff.Password);
        Api.Emails.To(address).Should().Contain(m => m.Subject == "An administrator reset your MedApp two-factor authentication");
    }

    [Test]
    public async Task AdminReset_OwnAccountUnknownUserAndStaff_AreRefused()
    {
        var admin = await SignInAsAdminAsync();
        var staff = await CreateStaffAsync(UserRole);

        (await admin.PostAsync($"/api/auth/users/{AdminUsername}/reset-two-factor", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/auth/users/nobody-at-all/reset-two-factor", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await staff.Client.PostAsync($"/api/auth/users/{AdminUsername}/reset-two-factor", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RequirementForAdmins_HoldsAnAdminWithoutItUntilTheySetItUp()
    {
        var admin = await SignInAsAdminAsync();
        var otherAdmin = await CreateStaffAsync(AdminRole, UserRole);
        var staff = await CreateStaffAsync(UserRole);

        // Not until the admin turning it on uses it themselves.
        (await admin.PutAsJsonAsync("/api/auth/security-settings", new SaveSecuritySettingsRequest(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var (adminKey, _) = await TurnOnTwoFactorAsync(admin, AdminPassword);
        var settings = await (await admin.PutAsJsonAsync("/api/auth/security-settings", new SaveSecuritySettingsRequest(true)))
            .ReadAsAsync<SecuritySettingsResponse>();
        settings.RequireTwoFactorForAdmins.Should().BeTrue();
        settings.AdminsWithoutTwoFactor.Should().Be(1);
        settings.UpdatedBy.Should().Be(AdminUsername);

        // The other admin is held from their next request; staff aren't.
        var whoAmI = await (await otherAdmin.Client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        whoAmI.MustSetUpTwoFactor.Should().BeTrue();
        var held = await otherAdmin.Client.GetAsync("/api/patients/search");
        held.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(held)).Should().Be(TwoFactorSetupRequired.Code);
        await (await staff.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Setting it up frees them.
        await TurnOnTwoFactorAsync(otherAdmin.Client, otherAdmin.Password);
        await (await otherAdmin.Client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // And it can't be turned off while it's required.
        var status = await (await admin.GetAsync("/api/auth/me/two-factor")).ReadAsAsync<TwoFactorStatusResponse>();
        status.Required.Should().BeTrue();
        (await admin.PostAsJsonAsync("/api/auth/me/two-factor/disable", new DisableTwoFactorRequest(AdminPassword, NextCode(adminKey))))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task RequirementForAdmins_SettingsAreForAdminsOnly()
    {
        var staff = await CreateStaffAsync(UserRole);

        (await staff.Client.GetAsync("/api/auth/security-settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.Client.PutAsJsonAsync("/api/auth/security-settings", new SaveSecuritySettingsRequest(false))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ActivityLog_RecordsTheChallengeTheRefusedCodeTheSignInAndTheChanges_NeverACode()
    {
        var staff = await CreateStaffAsync(UserRole);
        var (key, codes) = await TurnOnTwoFactorAsync(staff.Client, staff.Password);
        var client = NewClient();
        var challenge = await PasswordStepAsync(client, staff.Username, staff.Password);
        await SecondStepAsync(client, challenge.Challenge, "000000");
        var code = NextCode(key);
        await (await SecondStepAsync(client, challenge.Challenge, code)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var admin = await SignInAsAdminAsync();
        await (await admin.PostAsync($"/api/auth/users/{staff.Username}/reset-two-factor", null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var page = await (await admin.GetAsync($"/api/activity/search?PageSize=100&UserId={staff.UserId}"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        var actions = page.Items.Select(e => e.Action).ToList();

        actions.Should().Contain(["TwoFactorEnabled", "TwoFactorChallenged", "SignInFailed", "SignedIn"]);
        page.Items.Should().Contain(e => e.Action == "SignInFailed" && e.Details.Contains("InvalidCode"));
        page.Items.Should().Contain(e => e.Action == "SignedIn" && e.Details.Contains("authenticator"));
        page.Items.Should().OnlyContain(e => !e.Details.Contains(code) && !e.Details.Contains(codes[0]));

        var resets = await (await admin.GetAsync("/api/activity/search?PageSize=100&Actions=TwoFactorReset"))
            .ReadAsAsync<PagedResponse<ActivityEntryResponse>>();
        var reset = resets.Items.Should().ContainSingle().Subject;
        reset.Username.Should().Be(AdminUsername);
        reset.TargetUsername.Should().Be(staff.Username);
    }
}
