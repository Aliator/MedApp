using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.AccountSecurity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>
/// "Forgot your password?" end to end, with the emails kept by the fake sender: the same answer whatever matched, a
/// single-use link to the confirmed address, the new password's rules, and everything a reset ends.
/// </summary>
[TestFixture]
public sealed class PasswordResetTests : IntegrationTest
{
    private static Task<HttpResponseMessage> ForgotAsync(string usernameOrEmail) =>
        NewClient().PostAsJsonAsync("/api/auth/password/forgot", new ForgotPasswordRequest(usernameOrEmail));

    private static Task<HttpResponseMessage> ResetAsync(Guid userId, string token, string password) =>
        NewClient().PostAsJsonAsync("/api/auth/password/reset", new ResetPasswordRequest(userId, token, password));

    /// <summary>A staff account with a confirmed address, and the client it's signed in with.</summary>
    private static async Task<(StaffAccount Staff, string Address)> StaffWithAddressAsync()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, address);
        Api.Emails.Clear();
        return (staff, address);
    }

    [Test]
    public async Task Forgot_AnyAccountOrNone_GetsTheSameAnswer_AndOnlyAConfirmedAddressGetsAnEmail()
    {
        var (withAddress, address) = await StaffWithAddressAsync();
        var withoutAddress = await CreateStaffAsync(UserRole);

        var answers = new[]
        {
            await ForgotAsync(withAddress.Username),
            await ForgotAsync(address.ToUpperInvariant()),
            await ForgotAsync(withoutAddress.Username),
            await ForgotAsync("nobody-has-this-name"),
            await ForgotAsync(NewAddress()),
        };

        foreach (var answer in answers)
        {
            answer.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await answer.Content.ReadAsStringAsync()).Should().BeEmpty();
        }

        Api.Emails.Sent.Should().HaveCount(2).And.OnlyContain(m => m.To == address && m.Subject == EmailTemplates.ResetPasswordSubject);
    }

    [Test]
    public async Task Reset_EndToEnd_SetsThePasswordEndsEverySessionAndTellsTheOwner()
    {
        var (staff, address) = await StaffWithAddressAsync();
        var elsewhere = await SignInAsync(staff.Username, staff.Password);

        await ForgotAsync(staff.Username);
        var (userId, token) = FakeEmailSender.LinkIn(Api.Emails.To(address).Single());
        userId.Should().Be(staff.UserId);

        await (await ResetAsync(userId, token, AnotherChosenPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await ShouldBeSignedInAsync(staff.Client, false);
        await ShouldBeSignedInAsync(elsewhere, false);
        (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, staff.Password)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var signedIn = await SignInAsync(staff.Username, AnotherChosenPassword);
        await ShouldBeSignedInAsync(signedIn);
        Api.Emails.To(address).Should().Contain(m => m.Subject == "Your MedApp password was reset");
    }

    [Test]
    public async Task Reset_TheLinkWorksOnce()
    {
        var (staff, address) = await StaffWithAddressAsync();
        await ForgotAsync(staff.Username);
        var (userId, token) = FakeEmailSender.LinkIn(Api.Emails.To(address).Single());

        await (await ResetAsync(userId, token, AnotherChosenPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var again = await ResetAsync(userId, token, "YetAnotherPassw0rd");

        await again.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await again.Content.ReadFromJsonAsync<IdentityError[]>()).Should().ContainSingle(e => e.Code == "InvalidToken");
    }

    [Test]
    public async Task Reset_WrongTokenOrUnknownAccount_IsTheSameInvalidLink()
    {
        var (staff, _) = await StaffWithAddressAsync();

        var wrongToken = await ResetAsync(staff.UserId, "not-a-token", AnotherChosenPassword);
        var unknownUser = await ResetAsync(Guid.NewGuid(), "not-a-token", AnotherChosenPassword);

        (await wrongToken.Content.ReadAsStringAsync()).Should().Be(await unknownUser.Content.ReadAsStringAsync());
        (await wrongToken.Content.ReadFromJsonAsync<IdentityError[]>()).Should().ContainSingle(e => e.Code == "InvalidToken");
    }

    [Test]
    public async Task Reset_CurrentOrCommonPassword_IsRefusedAndTheLinkStillWorks()
    {
        var (staff, address) = await StaffWithAddressAsync();
        await ForgotAsync(staff.Username);
        var (userId, token) = FakeEmailSender.LinkIn(Api.Emails.To(address).Single());

        var same = await ResetAsync(userId, token, staff.Password);
        var common = await ResetAsync(userId, token, "Password1");

        (await same.Content.ReadFromJsonAsync<IdentityError[]>()).Should().ContainSingle(e => e.Code == "SamePassword");
        (await common.Content.ReadFromJsonAsync<IdentityError[]>()).Should().Contain(e => e.Code == "CommonPassword");
        await (await ResetAsync(userId, token, AnotherChosenPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Reset_ClearsTheForcedPasswordChangeAndEndsALockout()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, address);
        var admin = await SignInAsAdminAsync();
        // An admin's reset makes them choose their own password; then they lock themselves out.
        await (await admin.PutAsync($"/api/auth/users/{staff.Username}/reset-password", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        for (var i = 0; i < 5; i++)
            await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, WrongPassword));
        (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(staff.Username, WrongPassword)))
            .StatusCode.Should().Be(HttpStatusCode.Locked);

        Api.Emails.Clear();
        await ForgotAsync(address);
        var (userId, token) = FakeEmailSender.LinkIn(Api.Emails.To(address).Single());
        await (await ResetAsync(userId, token, AnotherChosenPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var client = await SignInAsync(staff.Username, AnotherChosenPassword);
        var whoAmI = await (await client.GetAsync("/api/auth/whoami")).ReadAsAsync<WhoAmIResponse>();
        whoAmI.MustChangePassword.Should().BeFalse();
        await (await client.GetAsync("/api/patients/search")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Reset_LinkMadeByOneCopyOfTheApi_OpensOnAnother()
    {
        var (staff, address) = await StaffWithAddressAsync();
        await ForgotAsync(staff.Username);
        var (userId, token) = FakeEmailSender.LinkIn(Api.Emails.To(address).Single());

        // A second copy of the API, sharing only the database (where the data protection keys are).
        await using var otherCopy = new MedAppApiFactory(TestEnvironment.ConnectionString);
        var response = await otherCopy.CreateApiClient().PostAsJsonAsync(
            "/api/auth/password/reset", new ResetPasswordRequest(userId, token, AnotherChosenPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        var keys = await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>().DataProtectionKeys.CountAsync());
        keys.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task Forgot_ManyRequestsForOneAccount_AreThrottled()
    {
        // The real limit (3 per username or address in 15 minutes), on an API of its own.
        await using var throttled = Api.WithWebHostBuilder(builder =>
            builder.UseSetting("LoginThrottling:ResetRequestsPerIdentifierPermitLimit", "3"));
        var client = throttled.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = MedAppApiFactory.BaseAddress });

        for (var i = 0; i < 3; i++)
            (await client.PostAsJsonAsync("/api/auth/password/forgot", new ForgotPasswordRequest("someone"))).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var refused = await client.PostAsJsonAsync("/api/auth/password/forgot", new ForgotPasswordRequest("SOMEONE"));

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.Should().NotBeNull();
    }
}
