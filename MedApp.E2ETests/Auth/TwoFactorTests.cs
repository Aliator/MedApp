using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Auth;

/// <summary>
/// Two-factor authentication, as a member of staff uses it: set up an authenticator app in Account Settings, Security
/// (the key shown is read from the page, and codes made from it as an app makes them), then sign in again in two steps.
/// Needs no email, so it runs against the production image as CI's e2e job starts it.
/// </summary>
[TestFixture]
public sealed class TwoFactorTests : E2ETest
{
    private static readonly string ChosenPassword = E2EData.NewPassword();

    [Test]
    public async Task SetUpTwoFactor_ThenSignInWithAPasswordAndACode()
    {
        // A member of staff with their own password (made through the API).
        var username = E2EData.UniqueName("twofactor").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await Api.CreateUserAsync(username, "All staff (before roles)");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        var theirApi = await SignInToApiAsync(username, firstPassword);
        await theirApi.UpdateOwnPasswordAsync(username, firstPassword, ChosenPassword);

        // They sign in, in a browser of their own, and set up two-factor from Account Settings, Security.
        var page = await NewSignedOutPageAsync();
        await SignInWithPasswordAsync(page, username);
        await Expect(page).Not.ToHaveURLAsync(new Regex("/login"));
        await OpenAsync(page, "/account/security");
        await page.GetByRole(AriaRole.Button, new() { Name = "Set Up Two-Factor" }).ClickAsync();

        await Expect(page.GetByRole(AriaRole.Img, new() { Name = "QR code for your authenticator app" })).ToBeVisibleAsync();
        var key = (await page.GetByText(new Regex("^[a-z2-7]{4}( [a-z2-7]{1,4}){3,}$")).TextContentAsync())!.Trim();
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Code from your authenticator app" }).FillAsync(CodeFor(key));
        // Turning it on needs the password again, so a session left open can't do it.
        await page.GetByLabel("Your password").FillAsync(ChosenPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Turn on two-factor" }).ClickAsync();

        // The recovery codes are shown once.
        await Expect(page.GetByText("Save your recovery codes now. They won't be shown again.")).ToBeVisibleAsync();
        // Turning it on ends the account's other sessions (this API one), and keeps the one it was turned on from.
        Assert.That((await theirApi.WhoAmIAsync()).IsAuthenticated, Is.False, "turning it on ends the account's other sessions");

        // Signing in again now takes the password, then a code from the app.
        var again = await NewSignedOutPageAsync();
        await SignInWithPasswordAsync(again, username);
        var codeField = again.GetByRole(AriaRole.Textbox, new() { Name = "Code from your authenticator app" });
        await Expect(codeField).ToBeVisibleAsync();
        await Expect(again).ToHaveURLAsync(new Regex("/login"));

        // The next step's code: the one used to turn it on can't be used again.
        await codeField.FillAsync(CodeFor(key, stepsAhead: 1));
        await again.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        var account = again.GetByRole(AriaRole.Navigation, new() { Name = "Side navigation" })
            .GetByRole(AriaRole.Link, new() { NameRegex = new Regex($"^{Regex.Escape(username)}\\b") });
        await Expect(account).ToBeVisibleAsync();
        await Expect(again).Not.ToHaveURLAsync(new Regex("/login"));
    }

    /// <summary>A new browser context with nobody signed in, and a page in it.</summary>
    private async Task<IPage> NewSignedOutPageAsync()
    {
        var options = ContextOptions();
        options.StorageStatePath = null;
        var context = await Browser.NewContextAsync(options);
        context.SetDefaultTimeout(15_000);
        return await context.NewPageAsync();
    }

    private static async Task SignInWithPasswordAsync(IPage page, string username)
    {
        await OpenAsync(page, "/login");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(ChosenPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
    }

    /// <summary>The code an authenticator app shows for <paramref name="key"/> (as the page shows it), now or a step ahead.</summary>
    private static string CodeFor(string key, int stepsAhead = 0) =>
        Totp.Compute(Base32.Decode(key.Replace(" ", string.Empty)), Totp.TimeStep(DateTimeOffset.UtcNow) + stepsAhead);
}
