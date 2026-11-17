using System.Text.Json;
using System.Text.RegularExpressions;
using MedApp.Contracts.Calendar.Responses;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>
/// Arranging a meeting from the team view: an admin narrows it to themselves and a member of staff, takes the first
/// suggested time in New Meeting, finds the meeting on the member of staff's calendar, and cancels it there.
/// </summary>
[TestFixture]
public sealed class MeetingTests : E2ETest
{
    [Test]
    public async Task NewMeeting_FromTheTeamViewAtASuggestedTime_ShowsOnAnAttendeesCalendarAndCanBeCancelled()
    {
        var staffUsername = E2EData.UniqueName("staff").Replace(' ', '-').ToLowerInvariant();
        await Api.CreateUserAsync(staffUsername, "User");
        Data.OnCleanUp($"user {staffUsername}", () => Api.DeleteUserAsync(staffUsername, E2ESettings.Password!));
        var staffId = await Api.UserIdOfAsync(staffUsername);
        var meId = await Api.UserIdOfAsync(E2ESettings.Username!);
        var title = E2EData.UniqueName("Meeting");

        // The team view, narrowed to the two of us, on the next weekday.
        var day = UkToday.AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        await OpenAsync($"/calendar?view=team&date={day:yyyy-MM-dd}&who={meId},{staffId}");
        var team = Main.GetByRole(AriaRole.Group, new() { NameRegex = new Regex("^Team, ") });
        await Expect(team).ToBeVisibleAsync();
        await Expect(team).ToContainTextAsync("Everyone free");
        await Expect(team).ToContainTextAsync(staffUsername);

        // New meeting, for the people shown, at the first suggested time.
        await Main.GetByRole(AriaRole.Button, new() { Name = "New", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "New meeting" }).ClickAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "New Meeting" });
        await dialog.GetByRole(AriaRole.Textbox, new() { Name = "Title" }).FillAsync(title);
        var suggestions = dialog.GetByRole(AriaRole.Group, new() { Name = "Suggested times" });
        var first = suggestions.GetByRole(AriaRole.Button).First;
        await Expect(first).ToBeVisibleAsync();
        await first.ClickAsync();
        // Wait for the save itself, not for the dialog to hide: while it saves, the dialog is an alertdialog (Astryx's
        // purpose="required"), so a Dialog locator stops matching, and "hidden" is true, the moment Add Meeting is
        // pressed (README, "Waiting for a save"). The first meeting a fresh API saves can be slow.
        var saved = await Page.RunAndWaitForResponseAsync(
            () => dialog.GetByRole(AriaRole.Button, new() { Name = "Add Meeting" }).ClickAsync(),
            r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath == "/api/calendar/meetings",
            new() { Timeout = 30_000 });
        Assert.That(saved.Status, Is.EqualTo(201), "Adding the meeting should succeed.");
        var meeting = JsonSerializer.Deserialize<MeetingResponse>(await saved.TextAsync(), JsonSerializerOptions.Web)!;
        // Registered at once, so the meeting goes however the test ends (cancelling it twice is fine).
        Data.OnCleanUp($"meeting {title}", () => Api.CancelMeetingAsync(meeting.Id));
        await Expect(dialog).ToBeHiddenAsync();

        // It's on the member of staff's calendar, with both of us going.
        var onTheirs = (await Api.CalendarAsync(staffId, meeting.Date, meeting.Date)).Meetings.Single(m => m.Id == meeting.Id);
        Assert.That(onTheirs.Title, Is.EqualTo(title));
        Assert.That(onTheirs.Attendees.Select(a => a.UserId), Is.EquivalentTo(new[] { meId, staffId }));

        // Found on their calendar that day, opened, and cancelled.
        await OpenAsync($"/calendar?staff={staffId}&date={meeting.Date:yyyy-MM-dd}&view=day");
        var onCalendar = Main.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^{Regex.Escape(title)}, meeting, .*2 attending") });
        await Expect(onCalendar).ToBeVisibleAsync();
        // A clickable card: its button is for the keyboard and screen readers, and the card itself takes the click.
        await onCalendar.Locator("xpath=..").ClickAsync();
        var details = Page.GetByRole(AriaRole.Dialog, new() { Name = "Meeting" });
        await Expect(details).ToContainTextAsync(title);
        await Expect(details).ToContainTextAsync(staffUsername);
        await details.GetByRole(AriaRole.Button, new() { Name = "Cancel Meeting" }).ClickAsync();
        var confirm = Page.GetByRole(AriaRole.Dialog, new() { Name = "Cancel Meeting" });
        // As for the save: the confirmation is an alertdialog while it cancels, so wait for the cancel itself.
        var cancelled = await Page.RunAndWaitForResponseAsync(
            () => confirm.GetByRole(AriaRole.Button, new() { Name = "Cancel Meeting" }).ClickAsync(),
            r => r.Request.Method == "DELETE" && new Uri(r.Url).AbsolutePath == $"/api/calendar/meetings/{meeting.Id}");
        Assert.That(cancelled.Status, Is.EqualTo(204), "Cancelling the meeting should succeed.");
        await Expect(confirm).ToBeHiddenAsync();

        await Expect(onCalendar).ToBeHiddenAsync();
        var after = await Api.CalendarAsync(staffId, meeting.Date, meeting.Date);
        Assert.That(after.Meetings.Any(m => m.Id == meeting.Id), Is.False);
    }
}
