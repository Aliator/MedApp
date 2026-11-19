using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Users;

/// <summary>Admin's User Management: a new account's one-time password, and deleting an account.</summary>
[TestFixture]
public sealed class UserTests : E2ETest
{
    protected override E2EAccount SignedInAs => E2EAccount.Admin;

    /// <summary>The dialog's Done ignores presses for a second after the password appears (a double-click guard).</summary>
    private const int DoneHoldMs = 1100;

    /// <summary>The password the new user chooses: made for each run (never in the source), and meets the Identity rules.</summary>
    private static readonly string ChosenPassword = E2EData.NewPassword();

    /// <summary>
    /// A new account, from start to finish: the admin adds it and sees its first password once, gives it a role, then
    /// its user signs in with that password, must choose their own first, and gets into the app.
    /// </summary>
    [Test]
    public async Task AddUser_NewUserSignsInWithTheOneTimePasswordAndMustChooseTheirOwn()
    {
        var username = NewUsername();
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await OpenAsync("/admin/users");

        // The admin adds the account and is shown its first password, once.
        await Main.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create New User" });
        await dialog.GetByLabel("Username").FillAsync(username);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();

        await Expect(dialog).ToContainTextAsync($"Account created for {username}.");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Copy password" })).ToBeFocusedAsync();
        var shown = await dialog.GetByText(new Regex("^[A-Za-z0-9!@#$%]{16,}$")).TextContentAsync();
        Assert.That(shown, Is.Not.Null.And.Length.GreaterThanOrEqualTo(16));
        var firstPassword = shown!.Trim();

        await Page.WaitForTimeoutAsync(DoneHoldMs);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(Page.GetByText(firstPassword)).ToHaveCountAsync(0);

        // And gives it a role (an account with none can't change its password).
        await SearchUsersAsync(username);
        var row = Main.GetByRole(AriaRole.Row).Filter(new() { HasText = username });
        await row.GetByRole(AriaRole.Button, new() { Name = "Edit roles" }).ClickAsync();
        var roles = Page.GetByRole(AriaRole.Dialog, new() { Name = "Edit Roles" });
        await roles.GetByRole(AriaRole.Checkbox, new() { Name = "Nurse", Exact = true }).CheckAsync();
        await roles.GetByRole(AriaRole.Button, new() { Name = "Save Roles" }).ClickAsync();
        await Expect(roles).ToBeHiddenAsync();
        await Expect(row).ToContainTextAsync("Nurse");

        // The new user, in a browser of their own, signs in with the password they were given...
        var options = ContextOptions();
        options.StorageStatePath = null;
        await using var theirs = await Browser.NewContextAsync(options);
        theirs.SetDefaultTimeout(15_000);
        var page = await theirs.NewPageAsync();
        await OpenAsync(page, "/login");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(firstPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        // ...and must choose their own before anything else...
        await Expect(page).ToHaveURLAsync(new Regex("/change-password$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Choose your password" })).ToBeVisibleAsync();
        await WaitUntilInteractiveAsync(page);
        await page.GetByRole(AriaRole.Textbox, new() { NameRegex = new Regex("^Password you were given") }).FillAsync(firstPassword);
        await page.GetByRole(AriaRole.Textbox, new() { NameRegex = new Regex("^New password") }).FillAsync(ChosenPassword);
        await page.GetByRole(AriaRole.Textbox, new() { NameRegex = new Regex("^Confirm new password") }).FillAsync(ChosenPassword);
        await page.GetByRole(AriaRole.Button, new() { Name = "Change Password" }).ClickAsync();

        // ...then they're in the app.
        var account = page.GetByRole(AriaRole.Navigation, new() { Name = "Side navigation" })
            .GetByRole(AriaRole.Link, new() { NameRegex = new Regex($"^{Regex.Escape(username)}\\b") });
        await Expect(account).ToBeVisibleAsync();
        await Expect(page).Not.ToHaveURLAsync(new Regex("/(change-password|login)"));

        // Their own password is the one that works now, and nothing more is asked of them.
        var theirApi = await SignInToApiAsync(username, ChosenPassword);
        var me = await theirApi.WhoAmIAsync();
        Assert.That(me.MustChangePassword, Is.False);
    }

    [Test]
    public async Task DeleteUser_ConfirmedWithYourPassword_RemovesTheAccount()
    {
        var username = NewUsername();
        await Api.CreateUserAsync(username, "Nurse");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await OpenAsync("/admin/users");
        await SearchUsersAsync(username);
        var row = Main.GetByRole(AriaRole.Row).Filter(new() { HasText = username });

        await row.GetByRole(AriaRole.Button, new() { Name = "Delete user" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Confirm Delete" });
        await Expect(dialog).ToContainTextAsync(username);
        await dialog.GetByLabel("Your password").FillAsync(E2ESettings.Password!);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete User" }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        await Expect(row).ToHaveCountAsync(0);
        Assert.That(await Api.FindUserAsync(username), Is.Null);
    }

    private static string NewUsername() => E2EData.UniqueName("user").Replace(' ', '-').ToLowerInvariant();

    /// <summary>"User contains ..." in the list's filter bar.</summary>
    private async Task SearchUsersAsync(string username)
    {
        await (await OpenListSearchAsync("Search users")).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "User", Exact = true }).ClickAsync();
        await Main.GetByRole(AriaRole.Textbox, new() { Name = "Value" }).FillAsync(username);
        await Main.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
    }
}
