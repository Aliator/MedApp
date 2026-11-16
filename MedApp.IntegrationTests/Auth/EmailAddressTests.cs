using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Email;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using static MedApp.IntegrationTests.Common.AccountSecurity;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Auth;

/// <summary>Staff email addresses: set by an admin or by their owner, used only once confirmed by the emailed link.</summary>
[TestFixture]
public sealed class EmailAddressTests : IntegrationTest
{
    [Test]
    public async Task Features_SayEmailCanBeSent_ToAnyone()
    {
        var features = await (await NewClient().GetAsync("/api/auth/features")).ReadAsAsync<AuthFeaturesResponse>();

        features.CanSendEmail.Should().BeTrue();
    }

    [Test]
    public async Task AdminCreatesAnAccountWithAnAddress_ItsSentALinkNeverThePassword_AndCountsOnceConfirmed()
    {
        var admin = await SignInAsAdminAsync();
        var address = NewAddress();
        var username = UniqueName("emailed");

        var created = await (await admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(username, address))).ReadAsAsync<CreateUserResponse>();

        var email = Api.Emails.To(address).Should().ContainSingle().Subject;
        email.Subject.Should().Be(EmailTemplates.ConfirmEmailSubject);
        email.TextBody.Should().NotContain(created.Password);
        email.HtmlBody.Should().NotContain(created.Password);
        var pending = await (await admin.GetAsync($"/api/auth/users/{username}")).ReadAsAsync<UserResponse>();
        pending.Email.Should().BeNull();
        pending.PendingEmail.Should().Be(address);

        await ConfirmFromEmailAsync(address);

        var confirmed = await (await admin.GetAsync($"/api/auth/users/{username}")).ReadAsAsync<UserResponse>();
        confirmed.Email.Should().Be(address);
        confirmed.PendingEmail.Should().BeNull();
    }

    [Test]
    public async Task AdminCreatesAnAccountWithNotAnAddress_IsAValidationError()
    {
        var admin = await SignInAsAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/auth/users", new CreateUserRequest(UniqueName("bad"), "not-an-address"));

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email");
    }

    [Test]
    public async Task ChangingAnAddress_KeepsTheOldOneUntilTheNewOneIsConfirmed_ThenTellsTheOldOne()
    {
        var staff = await CreateStaffAsync(UserRole);
        var first = NewAddress();
        await ConfirmAddressAsync(staff.Client, first);
        var second = NewAddress();

        var waiting = await (await staff.Client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(second)))
            .ReadAsAsync<EmailResponse>(HttpStatusCode.Accepted);
        waiting.Should().Be(new EmailResponse(first, second));

        await ConfirmFromEmailAsync(second);

        (await (await staff.Client.GetAsync("/api/auth/me/email")).ReadAsAsync<EmailResponse>()).Should().Be(new EmailResponse(second, null));
        Api.Emails.To(first).Should().Contain(m => m.Subject == "Your MedApp email address was changed");
    }

    [Test]
    public async Task AConfirmationLink_WorksOnceAndOnlyForItsAddress()
    {
        var staff = await CreateStaffAsync(UserRole);
        var first = NewAddress();
        await (await staff.Client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(first))).ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        var (userId, firstToken) = FakeEmailSender.LinkIn(Api.Emails.To(first).Single());

        // Asking for another address replaces the pending one, so the first link no longer works.
        await (await staff.Client.PutAsJsonAsync("/api/auth/me/email", new ChangeEmailRequest(NewAddress()))).ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        var stale = await NewClient().PostAsJsonAsync("/api/auth/email/confirm", new ConfirmEmailRequest(userId, firstToken));

        await stale.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await stale.Content.ReadFromJsonAsync<IdentityError[]>()).Should().ContainSingle(e => e.Code == "InvalidToken");
    }

    [Test]
    public async Task RemovingTheAddress_TellsItAndStopsResetLinks()
    {
        var staff = await CreateStaffAsync(UserRole);
        var address = NewAddress();
        await ConfirmAddressAsync(staff.Client, address);

        await (await staff.Client.DeleteAsync("/api/auth/me/email")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        Api.Emails.To(address).Should().Contain(m => m.Subject == "Your MedApp email address was removed");
        Api.Emails.Clear();
        await NewClient().PostAsJsonAsync("/api/auth/password/forgot", new ForgotPasswordRequest(staff.Username));

        Api.Emails.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task OwnAddress_IsForStaffOnly()
    {
        (await NewClient().GetAsync("/api/auth/me/email")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
