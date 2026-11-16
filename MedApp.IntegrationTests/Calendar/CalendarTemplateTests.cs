using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.IntegrationTests.Common;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Day and week templates applied when a calendar is read: across a UK clock change, changed for one day (an exception
/// and a stored block, or an override), and edited from today while past days keep what they had.
/// </summary>
[TestFixture]
public sealed class CalendarTemplateTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    /// <summary>
    /// A 12:00 lunch is at 12:00 UK time on both sides of the next clock change: 12:00Z in GMT, 11:00Z in BST. The
    /// clocks change on the last Sunday of March (forward) and October (back).
    /// </summary>
    [Test]
    public async Task GetCalendar_AcrossTheNextClockChange_KeepsLunchAtNoonUkTime()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var (clockChange, springsForward) = NextClockChange(UkToday);
        var fridayBefore = clockChange.AddDays(-2);
        var mondayAfter = clockChange.AddDays(1);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, fridayBefore, mondayAfter);

        calendar.Days.Select(d => (d.Date, d.IsWorkingDay)).Should().Equal(
            (fridayBefore, true), (fridayBefore.AddDays(1), false), (clockChange, false), (mondayAfter, true));
        var lunches = calendar.Blocks.Where(b => b.IsFromTemplate).OrderBy(b => b.StartUtc).ToList();
        lunches.Should().HaveCount(2);
        var (fridayHour, mondayHour) = springsForward ? (12, 11) : (11, 12);
        lunches[0].Should().BeEquivalentTo(new
        {
            Date = fridayBefore,
            ActivityName = "Lunch",
            StartUtc = Utc(fridayBefore, fridayHour),
            EndUtc = Utc(fridayBefore, fridayHour + 1),
        });
        lunches[1].Should().BeEquivalentTo(new
        {
            Date = mondayAfter,
            StartUtc = Utc(mondayAfter, mondayHour),
            EndUtc = Utc(mondayAfter, mondayHour + 1),
        });
        // The working day moves with the clocks too: 08:00 UK time.
        calendar.Days[0].WorkStartUtc.Should().Be(Utc(fridayBefore, fridayHour - 4));
        calendar.Days[3].WorkStartUtc.Should().Be(Utc(mondayAfter, mondayHour - 4));
    }

    [Test]
    public async Task ChangeTemplateBlockForDay_StoresAnExceptionAndABlockForThatDayOnly()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var monday = MondayAWeekAhead();
        var tuesday = monday.AddDays(1);
        var lunch = await TemplateLunchAsync(monday);

        var changed = await (await _staff.Client.PutAsJsonAsync(
                TemplateBlockUrl(monday, lunch.TemplateBlockId!.Value),
                new UpdateCalendarBlockRequest { ActivityTypeId = LunchActivityTypeId, StartUtc = UkAt(monday, 12, 30), EndUtc = UkAt(monday, 13, 30) }))
            .ReadAsAsync<CalendarBlockResponse>();

        changed.Should().BeEquivalentTo(new { IsFromTemplate = false, StartUtc = UkAt(monday, 12, 30), ActivityName = "Lunch" });
        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, monday, tuesday);
        calendar.Blocks.Where(b => b.Date == monday).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { IsFromTemplate = false, BlockId = changed.BlockId, StartUtc = UkAt(monday, 12, 30) });
        calendar.Blocks.Where(b => b.Date == tuesday).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { IsFromTemplate = true, StartUtc = UkAt(tuesday, 12) });
    }

    [Test]
    public async Task ChangeTemplateBlockForDay_IntoAnAppointment_Returns409()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var monday = MondayAWeekAhead();
        var patient = await CreatePatientAsync(_staff.Client);
        await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(monday, 14));
        var lunch = await TemplateLunchAsync(monday);

        var response = await _staff.Client.PutAsJsonAsync(
            TemplateBlockUrl(monday, lunch.TemplateBlockId!.Value),
            new UpdateCalendarBlockRequest { ActivityTypeId = LunchActivityTypeId, StartUtc = UkAt(monday, 13, 30), EndUtc = UkAt(monday, 14, 30) });

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await TemplateLunchAsync(monday)).StartUtc.Should().Be(UkAt(monday, 12));
    }

    [Test]
    public async Task RemoveTemplateBlockForDay_LeavesItOffThatDayOnly()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var monday = MondayAWeekAhead();
        var lunch = await TemplateLunchAsync(monday);

        await (await _staff.Client.DeleteAsync(TemplateBlockUrl(monday, lunch.TemplateBlockId!.Value)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, monday, monday.AddDays(1));
        calendar.Blocks.Should().ContainSingle().Which.Date.Should().Be(monday.AddDays(1));
        // The freed time can be booked now.
        var patient = await CreatePatientAsync(_staff.Client);
        await (await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(monday, 12)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task ApplyDayTemplate_ThenClear_UsesItForThatDayThenGoesBackToTheWeeks()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var training = await CreateDayTemplateAsync(_admin, TemplateBlock(9, 10, TrainingActivityTypeId));
        var wednesday = MondayAWeekAhead().AddDays(2);
        // A block added by hand that day stays whatever its template.
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(wednesday, 16), UkAt(wednesday, 17));

        var day = await (await _staff.Client.PutAsJsonAsync(DayTemplateUrl(wednesday), new ApplyDayTemplateRequest { DayTemplateId = training.Id }))
            .ReadAsAsync<CalendarDayResponse>();

        day.Should().BeEquivalentTo(new { Date = wednesday, DayTemplateId = (Guid?)training.Id, DayTemplateName = training.Name, IsDayTemplateOverridden = true });
        var applied = await GetCalendarAsync(_staff.Client, _staff.UserId, wednesday);
        applied.Blocks.Select(b => (b.ActivityName, b.IsFromTemplate, b.StartUtc)).Should().BeEquivalentTo([
            ("Training", true, UkAt(wednesday, 9)),
            ("Training", false, UkAt(wednesday, 16)),
        ]);

        await (await _staff.Client.DeleteAsync(DayTemplateUrl(wednesday))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var cleared = await GetCalendarAsync(_staff.Client, _staff.UserId, wednesday);
        cleared.Days.Single().IsDayTemplateOverridden.Should().BeFalse();
        cleared.Blocks.Select(b => (b.ActivityName, b.IsFromTemplate, b.StartUtc)).Should().BeEquivalentTo([
            ("Lunch", true, UkAt(wednesday, 12)),
            ("Training", false, UkAt(wednesday, 16)),
        ]);
    }

    [Test]
    public async Task ApplyDayTemplate_None_LeavesTheDayWithoutTemplateBlocks()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var thursday = MondayAWeekAhead().AddDays(3);

        await (await _staff.Client.PutAsJsonAsync(DayTemplateUrl(thursday), new ApplyDayTemplateRequest { DayTemplateId = null }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, thursday);
        calendar.Blocks.Should().BeEmpty();
        calendar.Days.Single().Should().BeEquivalentTo(new { DayTemplateId = (Guid?)null, IsDayTemplateOverridden = true });
    }

    /// <summary>
    /// Templates apply when a calendar is read, and history is kept by dating rows: an edit shows from today on, while
    /// days already past keep what applied then, and can't be changed by hand.
    /// </summary>
    [Test]
    public async Task UpdateDayTemplate_ChangesTodayOnButPastDaysKeepWhatTheyHad()
    {
        var monday = MondayAWeekAhead();
        var wednesday = monday.AddDays(2);
        var friday = monday.AddDays(4);
        Clock.SetUtcNow(UkAt(monday, 7));
        var day = await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));

        Clock.SetUtcNow(UkAt(wednesday, 7));
        await (await UpdateDayTemplateAsync(_admin, day, TemplateBlock(12, 13, startMinute: 30, endMinute: 30)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, monday, friday);
        calendar.Blocks.OrderBy(b => b.StartUtc).Select(b => (b.Date, UkTimeOf(b.StartUtc))).Should().Equal(
            (monday, new TimeOnly(12, 0)),
            (monday.AddDays(1), new TimeOnly(12, 0)),
            (wednesday, new TimeOnly(12, 30)),
            (monday.AddDays(3), new TimeOnly(12, 30)),
            (friday, new TimeOnly(12, 30)));
        calendar.Days.Select(d => (d.Date, d.IsPast, d.IsToday)).Take(3).Should().Equal(
            (monday, true, false), (monday.AddDays(1), true, false), (wednesday, false, true));
        calendar.Blocks.Where(b => b.Date < wednesday).Should().AllSatisfy(b => b.CanEdit.Should().BeFalse());

        // Past days don't change.
        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(monday, 15), UkAt(monday, 16)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var mondayLunch = calendar.Blocks.First(b => b.Date == monday);
        await (await _staff.Client.DeleteAsync(TemplateBlockUrl(monday, mondayLunch.TemplateBlockId!.Value)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.PutAsJsonAsync(DayTemplateUrl(monday), new ApplyDayTemplateRequest { DayTemplateId = null }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task SetDefaultWeek_ToNone_StopsItsTemplateBlocksFromToday()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var monday = MondayAWeekAhead();

        var none = await (await SetDefaultWeekAsync(_staff.Client, _staff.UserId, null)).ReadAsAsync<StaffWeekTemplateResponse>();

        none.WeekTemplateId.Should().BeNull();
        (await GetCalendarAsync(_staff.Client, _staff.UserId, monday)).Blocks.Should().BeEmpty();
    }

    private static (DateOnly Sunday, bool SpringsForward) NextClockChange(DateOnly today)
    {
        for (var year = today.Year; ; year++)
        {
            foreach (var month in new[] { 3, 10 })
            {
                var sunday = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                while (sunday.DayOfWeek != DayOfWeek.Sunday)
                    sunday = sunday.AddDays(-1);
                if (sunday > today)
                    return (sunday, month == 3);
            }
        }
    }

    private static DateTime Utc(DateOnly date, int hour) => date.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    private static TimeOnly UkTimeOf(DateTime utc) => UkTime.TimeOf(utc);

    private string TemplateBlockUrl(DateOnly date, Guid templateBlockId) =>
        $"/api/calendar/staff/{_staff.UserId}/days/{date:yyyy-MM-dd}/template-blocks/{templateBlockId}";

    private string DayTemplateUrl(DateOnly date) => $"/api/calendar/staff/{_staff.UserId}/days/{date:yyyy-MM-dd}/day-template";

    private async Task<CalendarBlockResponse> TemplateLunchAsync(DateOnly date) =>
        (await GetCalendarAsync(_staff.Client, _staff.UserId, date)).Blocks.Should().ContainSingle(b => b.IsFromTemplate).Subject;
}
