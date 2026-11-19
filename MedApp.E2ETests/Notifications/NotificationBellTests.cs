using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Notifications;

/// <summary>The bell beside MedApp: a new notification arrives live, and opens its pathway.</summary>
[TestFixture]
public sealed class NotificationBellTests : E2ETest
{
    [Test]
    public async Task AssignedByAColleague_ShowsLiveInTheBellAndOpensThePathway()
    {
        // A service manager of the clinic (Manager assigns pathways), who assigns the run's member of staff.
        var other = await CreateColleagueAsync(["Manager"]);
        var (template, _) = await CreateTemplateAsync(1);
        var patient = await CreatePatientAsync();
        var pathway = await CreatePathwayAsync(patient.Id, template.Id);
        Data.OnCleanUp("the test's notification (marked read)", async () =>
        {
            foreach (var n in (await Api.NotificationsAsync()).Items.Where(n => n.PathwayId == pathway.Id && !n.IsRead))
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

        // Someone else assigns the run's member of staff, while the page is open: no reload.
        await other.Api.AssignAsync(pathway.Id, Staff.UserId);

        var bell = nav.GetByRole(AriaRole.Button, new() { Name = BellName(unreadBefore + 1), Exact = true });
        await Expect(bell).ToBeVisibleAsync();
        await bell.ClickAsync();
        var list = Page.GetByRole(AriaRole.Dialog, new() { Name = "Notifications" });
        var item = list.GetByRole(AriaRole.Link).Filter(new() { HasText = pathway.Title });
        await Expect(item).ToContainTextAsync($"{other.Username} assigned you to this pathway.");
        // Marked new, beside the link.
        await Expect(list.GetByRole(AriaRole.Listitem).Filter(new() { HasText = pathway.Title })).ToContainTextAsync("New");

        await item.ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"/pathways/{pathway.Id}$"));
        await Expect(nav.GetByRole(AriaRole.Button, new() { Name = BellName(unreadBefore), Exact = true })).ToBeVisibleAsync();
        var stored = (await Api.NotificationsAsync()).Items.Single(n => n.PathwayId == pathway.Id);
        Assert.That(stored.IsRead, Is.True);
    }

    private static string BellName(int unread) => unread == 0 ? "Notifications" : $"Notifications, {unread} unread";
}
