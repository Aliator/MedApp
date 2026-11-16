using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Notifications;

/// <summary>The bell beside MedApp: a new notification arrives live, and opens its procedure.</summary>
[TestFixture]
public sealed class NotificationBellTests : E2ETest
{
    private const string ChosenPassword = "E2e-Chosen-Passw0rd";

    [Test]
    public async Task AssignedByAnotherAdmin_ShowsLiveInTheBellAndOpensTheProcedure()
    {
        var other = await CreateOtherAdminAsync();
        var (template, _) = await CreateTemplateAsync(1);
        var patient = await CreatePatientAsync();
        var procedure = await CreateProcedureAsync(patient.Id, template.Id);
        var me = await Api.UserIdOfAsync(E2ESettings.Username!);
        Data.OnCleanUp("the test's notification (marked read)", async () =>
        {
            foreach (var n in (await Api.NotificationsAsync()).Items.Where(n => n.ProcedureId == procedure.Id && !n.IsRead))
                await Api.MarkNotificationReadAsync(n.Id);
        });
        var unreadBefore = (await Api.NotificationsAsync()).UnreadCount;
        // The page's live connection is up once the hub answers its handshake.
        var hubConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Page.WebSocket += (_, socket) =>
        {
            if (socket.Url.Contains("/api/notifications/hub"))
                socket.FrameReceived += (_, _) => hubConnected.TrySetResult();
        };
        await OpenAsync("/");
        await hubConnected.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var nav = Page.GetByRole(AriaRole.Navigation, new() { Name = "Side navigation" });
        await Expect(nav.GetByRole(AriaRole.Button, new() { Name = BellName(unreadBefore), Exact = true })).ToBeVisibleAsync();

        // Someone else assigns the E2E account, while the page is open: no reload.
        await other.Api.AssignAsync(procedure.Id, me);

        var bell = nav.GetByRole(AriaRole.Button, new() { Name = BellName(unreadBefore + 1), Exact = true });
        await Expect(bell).ToBeVisibleAsync();
        await bell.ClickAsync();
        var list = Page.GetByRole(AriaRole.Dialog, new() { Name = "Notifications" });
        var item = list.GetByRole(AriaRole.Link).Filter(new() { HasText = procedure.Title });
        await Expect(item).ToContainTextAsync($"{other.Username} assigned you to this procedure.");
        // Marked new, beside the link.
        await Expect(list.GetByRole(AriaRole.Listitem).Filter(new() { HasText = procedure.Title })).ToContainTextAsync("New");

        await item.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"/procedures/{procedure.Id}$"));
        await Expect(nav.GetByRole(AriaRole.Button, new() { Name = BellName(unreadBefore), Exact = true })).ToBeVisibleAsync();
        var stored = (await Api.NotificationsAsync()).Items.Single(n => n.ProcedureId == procedure.Id);
        Assert.That(stored.IsRead, Is.True);
    }

    private static string BellName(int unread) => unread == 0 ? "Notifications" : $"Notifications, {unread} unread";

    /// <summary>
    /// A second admin, deleted after the test. They choose a password of their own first, as a new account must
    /// before anything else.
    /// </summary>
    private async Task<(string Username, E2EApi Api)> CreateOtherAdminAsync()
    {
        var username = E2EData.UniqueName("admin").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await Api.CreateUserAsync(username, "Admin", "User");
        Data.OnCleanUp($"user {username}", () => Api.DeleteUserAsync(username, E2ESettings.Password!));

        var firstSession = await SignInToApiAsync(username, firstPassword);
        await firstSession.UpdateOwnPasswordAsync(username, firstPassword, ChosenPassword);
        return (username, await SignInToApiAsync(username, ChosenPassword));
    }
}
