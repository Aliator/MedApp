using System.Globalization;
using System.Text.RegularExpressions;
using MedApp.E2ETests.Common;
using Microsoft.Playwright;

namespace MedApp.E2ETests.Calendar;

/// <summary>
/// Dragging over free time on the Calendar with the mouse: the menu that opens stays open (it used to close at once,
/// taking the release as a press outside it) beside the selection, Block time blocks the time dragged over, and the
/// block can be removed.
/// </summary>
[TestFixture]
public sealed class BlockTimeTests : E2ETest
{
    private static readonly CultureInfo UkEnglish = CultureInfo.GetCultureInfo("en-GB");
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    /// <summary>The hours tried, in order, for one that's free from start to end.</summary>
    private static readonly int[] Hours = [14, 10, 15, 11, 16, 9];

    [Test]
    public async Task BlockTime_DraggedOverFreeTime_OpensTheMenuNextToItAndBlocksThatTime()
    {
        var myId = await Api.UserIdOfAsync((await Api.WhoAmIAsync()).Name!);
        var (day, hour) = await FreeHourAsync(myId);
        var startUtc = Utc(day, hour);
        var endUtc = Utc(day, hour + 1);

        // Deletes the block however the test ends, if it was made (the test deletes it itself too).
        Data.OnCleanUp("the blocked hour", async () =>
        {
            var calendar = await Api.CalendarAsync(myId, day);
            foreach (var block in calendar.Blocks.Where(b => !b.IsFromTemplate && b.StartUtc == startUtc && b.EndUtc == endUtc))
                await Api.DeleteBlockAsync(block.BlockId!.Value);
        });

        await OpenAsync($"/calendar?date={day:yyyy-MM-dd}&view=day");
        var dayName = day.ToString("dddd d MMMM", UkEnglish);
        var first = Main.GetByRole(AriaRole.Button, new() { Name = $"{dayName}, {hour:00}:00, free", Exact = true });
        var last = Main.GetByRole(AriaRole.Button, new() { Name = $"{dayName}, {hour:00}:45, free", Exact = true });
        await last.ScrollIntoViewIfNeededAsync();
        var from = (await first.BoundingBoxAsync())!;
        var to = (await last.BoundingBoxAsync())!;

        // Press on the hour, drag down to its last quarter, and let go: the whole hour.
        await Page.Mouse.MoveAsync(from.X + 10, from.Y + from.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(to.X + 10, to.Y + to.Height / 2, new() { Steps = 6 });
        await Page.Mouse.UpAsync();

        // The menu opens for the hour selected, stays open, and sits next to the selection (not in the window's corner).
        var range = $"{hour:00}:00 to {hour + 1:00}:00";
        var menu = Page.GetByRole(AriaRole.Dialog, new() { Name = "Selected time" });
        var selection = Main.GetByRole(AriaRole.Button, new() { Name = $"Selected time, {range}" });
        await Expect(menu).ToBeVisibleAsync();
        await Expect(selection).ToHaveAttributeAsync("aria-expanded", "true");
        await Page.WaitForTimeoutAsync(500);
        await Expect(menu).ToBeVisibleAsync();
        // Next to the selection on any side (in day view a wide selection has it below, or above when there's no room
        // below; in week view beside), never off in the window's corner.
        var menuBox = (await menu.BoundingBoxAsync())!;
        var selectionBox = (await selection.BoundingBoxAsync())!;
        var gapX = Math.Max(0, Math.Max(menuBox.X - (selectionBox.X + selectionBox.Width), selectionBox.X - (menuBox.X + menuBox.Width)));
        var gapY = Math.Max(0, Math.Max(menuBox.Y - (selectionBox.Y + selectionBox.Height), selectionBox.Y - (menuBox.Y + menuBox.Height)));
        Assert.That(Math.Max(gapX, gapY), Is.LessThanOrEqualTo(12),
            $"The menu should open next to the selection: it was {gapX:0} px across and {gapY:0} px up or down from it.");
        await menu.GetByRole(AriaRole.Button, new() { Name = "Block time" }).ClickAsync();

        // Block Time, for that hour, with the first activity.
        var dialog = Page.GetByRole(AriaRole.Dialog, new() { Name = "Block Time" });
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Block Time", Exact = true }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();

        // It's on the calendar for that hour. Open it and delete it, leaving the calendar as it was.
        var block = Main.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(Regex.Escape(range)) });
        await Expect(block).ToBeVisibleAsync();
        // A clickable card: its button is for the keyboard and screen readers, and the card itself takes the click.
        await block.Locator("xpath=..").ClickAsync();
        var change = Page.GetByRole(AriaRole.Dialog, new() { Name = "Change Blocked Time" });
        await change.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(change).ToBeHiddenAsync();
        await Expect(block).ToBeHiddenAsync();
    }

    /// <summary>
    /// A working day three to six weeks ahead (clear of the other journeys' bookings and of time that has passed) and an
    /// hour on it with nothing on the calendar: no block (stored or from a template), appointment or meeting.
    /// </summary>
    private async Task<(DateOnly Day, int Hour)> FreeHourAsync(Guid staffUserId)
    {
        for (var day = UkToday.AddDays(21); day <= UkToday.AddDays(42); day = day.AddDays(1))
        {
            var calendar = await Api.CalendarAsync(staffUserId, day);
            if (calendar.Days.SingleOrDefault() is not { IsWorkingDay: true, IsPast: false })
                continue;

            foreach (var hour in Hours)
            {
                var start = Utc(day, hour);
                var end = Utc(day, hour + 1);
                var taken = calendar.Blocks.Any(b => b.StartUtc < end && start < b.EndUtc)
                            || calendar.Appointments.Any(a => a.StartUtc < end && start < a.EndUtc)
                            || calendar.Meetings.Any(m => m.StartUtc < end && start < m.EndUtc);
                if (!taken)
                    return (day, hour);
            }
        }

        Assert.Fail("No free hour on the calendar three to six weeks ahead.");
        return default;
    }

    private static DateTime Utc(DateOnly day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0)), London);
}
