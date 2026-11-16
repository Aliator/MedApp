using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Users;

/// <summary>Admin's User Management: a new account's one-time password, and deleting an account.</summary>
[TestFixture]
public sealed class UserTests : E2ETest
{
    /// <summary>The dialog's Done ignores presses for a second after the password appears (a double-click guard).</summary>
    private const int DoneHoldMs = 1100;

    [Test]
    public async Task AddUser_ShowsAOneTimePasswordThatSignsIn()
    {
        var username = NewUsername();
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));
        await OpenAsync("/admin/users");

        await Main.GetByRole(AriaRole.Button, new() { Name = "Add User" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Create New User" });
        await dialog.GetByLabel("Username").FillAsync(username);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create User" }).ClickAsync();

        await Expect(dialog).ToContainTextAsync($"Account created for {username}.");
        await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Copy password" })).ToBeFocusedAsync();
        var password = await dialog.GetByText(new Regex("^[A-Za-z0-9!@#$%]{16,}$")).TextContentAsync();
        Assert.That(password, Is.Not.Null.And.Length.GreaterThanOrEqualTo(16));

        // The password shown is the account's: it signs in.
        await SignInToApiAsync(username, password!.Trim());

        await Page.WaitForTimeoutAsync(DoneHoldMs);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        // Shown once: it's gone from the page.
        await Expect(Page.GetByText(password)).ToHaveCountAsync(0);
        await SearchUsersAsync(username);
        await Expect(Main.GetByRole(AriaRole.Row).Filter(new() { HasText = username })).ToHaveCountAsync(1);
    }

    [Test]
    public async Task DeleteUser_ConfirmedWithYourPassword_RemovesTheAccount()
    {
        var username = NewUsername();
        await Api.CreateUserAsync(username, "User");
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
        await Main.GetByRole(AriaRole.Combobox, new() { Name = "Search users" }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "User", Exact = true }).ClickAsync();
        await Main.GetByRole(AriaRole.Textbox, new() { Name = "Value" }).FillAsync(username);
        await Main.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();
    }
}
