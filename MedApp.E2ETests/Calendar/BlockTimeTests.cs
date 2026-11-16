using System.Globalization;
using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>
/// Dragging over free time on the Calendar with the mouse: the menu that opens stays open (it used to close at once,
/// taking the release as a press outside it), Block time blocks the time dragged over, and the block can be removed.
/// </summary>
[TestFixture]
public sealed class BlockTimeTests : E2ETest
{
    private static readonly CultureInfo UkEnglish = CultureInfo.GetCultureInfo("en-GB");

    [Test]
    public async Task BlockTime_DraggedOverFreeTime_OpensTheMenuAndBlocksThatTime()
    {
        // A weekday three weeks ahead, clear of the other journeys' bookings and of time that has passed.
        var day = UkToday.AddDays(21);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        await OpenAsync($"/calendar?date={day:yyyy-MM-dd}&view=day");

        var dayName = day.ToString("dddd d MMMM", UkEnglish);
        var first = Main.GetByRole(AriaRole.Button, new() { Name = $"{dayName}, 14:00, free", Exact = true });
        var last = Main.GetByRole(AriaRole.Button, new() { Name = $"{dayName}, 14:45, free", Exact = true });
        await last.ScrollIntoViewIfNeededAsync();
        var from = (await first.BoundingBoxAsync())!;
        var to = (await last.BoundingBoxAsync())!;

        // Press on 14:00, drag down to 14:45, and let go: an hour, to 15:00.
        await Page.Mouse.MoveAsync(from.X + 10, from.Y + from.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to.X + 10, to.Y + to.Height / 2, new() { Steps = 6 });
        await Page.Mouse.UpAsync();

        // The menu opens for the hour selected, and stays open.
        var menu = Page.GetByRole(AriaRole.Dialog, new() { Name = "Selected time" });
        await Expect(menu).ToBeVisibleAsync();
        await Expect(Main).ToContainTextAsync("14:00 to 15:00");
        await Page.WaitForTimeoutAsync(500);
        await Expect(menu).ToBeVisibleAsync();
        await menu.GetByRole(AriaRole.Button, new() { Name = "Block time" }).ClickAsync();

        // Block Time, for that hour, with the first activity.
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Block Time" });
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Block Time", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();

        // It's on the calendar for that hour. Open it and delete it, leaving the calendar as it was.
        var block = Main.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("14:00 to 15:00") });
        await Expect(block).ToBeVisibleAsync();
        // A clickable card: its button is for the keyboard and screen readers, and the card itself takes the click.
        await block.Locator("xpath=..").ClickAsync();
        var change = Page.GetByRole(AriaRole.Dialog, new() { Name = "Change Blocked Time" });
        await change.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(change).ToBeHiddenAsync();
        await Expect(block).ToBeHiddenAsync();
    }
}
