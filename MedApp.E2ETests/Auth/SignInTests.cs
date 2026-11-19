using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Auth;

/// <summary>Signing in and out through the login page, as the E2E admin.</summary>
[TestFixture]
public sealed class SignInTests : E2ETest
{
    protected override bool IsSignedIn => false;

    protected override E2EAccount SignedInAs => E2EAccount.Admin;

    [Test]
    public async Task SignIn_RightPassword_OpensTheAppAndSignOutReturnsToLogin()
    {
        await OpenAsync("/login");

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(E2ESettings.Username!);
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(E2ESettings.Password!);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        var account = Page.GetByRole(AriaRole.Navigation, new() { Name = "Side navigation" })
            .GetByRole(AriaRole.Link, new() { NameRegex = new Regex($"^{Regex.Escape(E2ESettings.Username!)}\\b") });
        await Expect(account).ToBeVisibleAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("/login"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign Out" }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog, new() { Name = "Sign Out" })
            .GetByRole(AriaRole.Button, new() { Name = "Sign Out" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/login$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in" })).ToBeVisibleAsync();
        // Signed out for real: the app's pages send you back to sign in.
        await OpenAsync("/patients");
        await Expect(Page).ToHaveURLAsync(new Regex("/login"));
    }

    [Test]
    public async Task SignIn_WrongPassword_SaysInvalidCredentialsAndStaysOnLogin()
    {
        // An account of its own, so a wrong password never counts towards locking out the E2E account.
        var username = E2EData.UniqueName("Login").Replace(' ', '-').ToLowerInvariant();
        await Api.CreateUserAsync(username, "Nurse");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await OpenAsync("/login");

        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync("Not-the-passw0rd");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "Username or password is incorrect." })).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/login$"));
    }
}
