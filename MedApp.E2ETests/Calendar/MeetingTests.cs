using System.Text.RegularExpressions;
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
        // Deleting the account also takes it off any meeting the test leaves behind.
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
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add Meeting" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();

        // It's on the member of staff's calendar, with both of us going.
        var meeting = (await Api.CalendarAsync(staffId, UkToday, UkToday.AddDays(20))).Meetings.Single(m => m.Title == title);
        Data.OnCleanUp($"meeting {title}", () => Api.CancelMeetingAsync(meeting.Id));
        Assert.That(meeting.Attendees.Select(a => a.UserId), Is.EquivalentTo(new[] { meId, staffId }));

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
        await confirm.GetByRole(AriaRole.Button, new() { Name = "Cancel Meeting" }).ClickAsync();
        await Expect(confirm).ToBeHiddenAsync();

        await Expect(onCalendar).ToBeHiddenAsync();
        var after = await Api.CalendarAsync(staffId, meeting.Date, meeting.Date);
        Assert.That(after.Meetings.Any(m => m.Id == meeting.Id), Is.False);
    }
}
