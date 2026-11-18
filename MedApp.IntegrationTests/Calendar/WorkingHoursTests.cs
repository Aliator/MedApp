using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Sites.Common;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Sites.Requests;
using MedApp.Contracts.Sites.Responses;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Working hours per site and per person (centre plan decisions 119 to 122), through the API against SQL Server: the
/// dated rows, who may set them, the working day every calendar command checks (a Saturday clinic, an evening, a site's
/// closed day), and the site rules that keep staff hours inside their site's.
/// </summary>
[TestFixture]
public sealed class WorkingHoursTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private SiteResponse _site = null!;
    private DateOnly _saturday;
    private DateOnly _monday;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _site = await CreateSiteAsync(_admin);
        _monday = MondayAWeekAhead();
        _saturday = _monday.AddDays(5);
    }

    private static string StaffHoursUrl(Guid userId) => $"/api/calendar/staff/{userId}/hours";

    private string SiteHoursUrl => $"/api/sites/{_site.Id}/hours";

    private Task<HttpResponseMessage> PutSiteHoursAsync(params SiteHoursDayRequest[] days) =>
        _admin.PutAsJsonAsync(SiteHoursUrl, new SiteHoursRequest { Days = [.. days] });

    private Task<HttpResponseMessage> PutStaffHoursAsync(HttpClient client, params StaffHoursDayRequest[] days) =>
        client.PutAsJsonAsync(StaffHoursUrl(_staff.UserId), new StaffHoursRequest { Days = [.. days] });

    private StaffHoursDayRequest Day(string day, int starts, int ends) =>
        new() { DayOfWeek = day, Starts = new TimeOnly(starts, 0), Ends = new TimeOnly(ends, 0), SiteId = _site.Id };

    private static SiteHoursDayRequest Open(string day, int opens, int closes) =>
        new() { DayOfWeek = day, Opens = new TimeOnly(opens, 0), Closes = new TimeOnly(closes, 0) };

    /// <summary>The site open Monday 08:00 to 18:00 and Saturday 08:00 to 13:00; they work Monday 09:00 to 17:00 and Saturday 09:00 to 13:00.</summary>
    private async Task GiveThemSaturdayMorningsAsync()
    {
        await (await PutSiteHoursAsync(Open("Monday", 8, 18), Open("Saturday", 8, 13))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PutStaffHoursAsync(_admin, Day("Monday", 9, 17), Day("Saturday", 9, 13))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    private static async Task<IEnumerable<string>> ErrorsAsync(HttpResponseMessage response) =>
        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e);

    [Test]
    public async Task Hours_NoneOfTheirOwn_AreTheDefault_AndAnyoneCanReadThem()
    {
        var hours = await (await _staff.Client.GetAsync(StaffHoursUrl(_staff.UserId))).ReadAsAsync<StaffHoursResponse>();
        hours.HasOwnHours.Should().BeFalse();
        hours.CanEdit.Should().BeFalse();
        hours.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");

        var site = await (await _staff.Client.GetAsync(SiteHoursUrl)).ReadAsAsync<SiteHoursResponse>();
        site.HasOwnHours.Should().BeFalse();
        site.Days.Should().HaveCount(5);

        await (await _staff.Client.GetAsync(StaffHoursUrl(Guid.NewGuid()))).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Hours_SetByManageClinicsOrManageAnyCalendar_NeverByTheirOwnerWithoutEither()
    {
        await (await PutStaffHoursAsync(_staff.Client, Day("Monday", 9, 17))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PutAsJsonAsync(SiteHoursUrl, new SiteHoursRequest())).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        // The Booking role has both ManageClinics and ManageAnyCalendar; the Manager role has ManageClinics only.
        var booking = await CreateStaffAsync("Booking");
        var manager = await CreateStaffAsync("Manager");
        (await (await PutStaffHoursAsync(booking.Client, Day("Monday", 9, 17))).ReadAsAsync<StaffHoursResponse>())
            .Days.Should().ContainSingle().Which.SiteName.Should().Be(_site.Name);
        await (await PutStaffHoursAsync(manager.Client, Day("Tuesday", 9, 17))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await manager.Client.PutAsJsonAsync(SiteHoursUrl, new SiteHoursRequest())).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        var now = await (await _staff.Client.GetAsync(StaffHoursUrl(_staff.UserId))).ReadAsAsync<StaffHoursResponse>();
        now.HasOwnHours.Should().BeTrue();
        now.Since.Should().Be(UkToday);
        now.Days.Select(d => d.DayOfWeek).Should().Equal("Tuesday");
    }

    [Test]
    public async Task ASaturdayClinic_TheirCalendarWorksSaturday_AndNotTheDaysTheyDont()
    {
        await GiveThemSaturdayMorningsAsync();

        var week = await GetCalendarAsync(_staff.Client, _staff.UserId, _monday, _monday.AddDays(6));
        week.Days.Select(d => d.IsWorkingDay).Should().Equal(true, false, false, false, false, true, false);
        var saturday = week.Days[5];
        saturday.HasHours.Should().BeTrue();
        saturday.SiteId.Should().Be(_site.Id);
        saturday.WorkStartUtc.Should().Be(UkAt(_saturday, 9));
        saturday.WorkEndUtc.Should().Be(UkAt(_saturday, 13));
        saturday.GridStartUtc.Should().Be(UkAt(_saturday, 9));
        saturday.GridEndUtc.Should().Be(UkAt(_saturday, 17), "every day's grid fits the longest day shown");
        week.WorkingDayStart.Should().Be("09:00");

        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_saturday, 10), UkAt(_saturday, 11))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await ErrorsAsync(await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_saturday, 12), UkAt(_saturday, 14))))
            .Should().Contain(WorkingHours.Within(new TimeOnly(9, 0), new TimeOnly(13, 0)));
        (await ErrorsAsync(await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_monday.AddDays(1), 10), UkAt(_monday.AddDays(1), 11))))
            .Should().Contain(WorkingHours.NotWorking(DayOfWeek.Tuesday));

        var patient = await CreatePatientAsync(_staff.Client);
        await (await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_saturday, 11))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await ErrorsAsync(await PostBookingAsync(_staff.Client, patient.Id, _staff.UserId, UkAt(_monday, 8))))
            .Should().Contain(WorkingHours.Within(new TimeOnly(9, 0), new TimeOnly(17, 0)));
    }

    [Test]
    public async Task MovingABlock_OutsideTheOwnersHours_Is400()
    {
        await GiveThemSaturdayMorningsAsync();
        var block = await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_saturday, 10), UkAt(_saturday, 11));

        (await ErrorsAsync(await PutBlockAsync(_staff.Client, block.BlockId!.Value, UkAt(_saturday, 13), UkAt(_saturday, 14))))
            .Should().Contain(WorkingHours.Within(new TimeOnly(9, 0), new TimeOnly(13, 0)));
        await (await PutBlockAsync(_staff.Client, block.BlockId!.Value, UkAt(_monday, 10), UkAt(_monday, 11))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task AMeeting_OutsideAnAttendeesHours_Is400NamingThem()
    {
        await GiveThemSaturdayMorningsAsync();

        var response = await _admin.PostAsJsonAsync("/api/calendar/meetings", new MeetingRequest
        {
            Title = "Team huddle",
            StartUtc = UkAt(_monday, 8),
            EndUtc = UkAt(_monday, 9),
            AttendeeUserIds = [_staff.UserId]
        });

        (await ErrorsAsync(response)).Should().Contain(WorkingHours.Within(new TimeOnly(9, 0), new TimeOnly(17, 0), _staff.Username));
    }

    [Test]
    public async Task ASitesClosedDay_ClosesTheCalendarsOfStaffWorkingThereThatDay_Only()
    {
        await GiveThemSaturdayMorningsAsync();
        var other = await CreateStaffAsync(UserRole);
        await AddClosedDayAsync(_admin, _monday, "Fire drill", _site.Id);

        (await ErrorsAsync(await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_monday, 10), UkAt(_monday, 11))))
            .Should().Contain(WorkingHours.ClosedDay(_monday, "Fire drill"));
        var day = (await GetCalendarAsync(_staff.Client, _staff.UserId, _monday)).Days.Single();
        day.IsWorkingDay.Should().BeFalse();
        day.ClosedDayName.Should().Be("Fire drill");

        // Someone on the default works at no particular site, so the site's day doesn't close theirs.
        await (await PostBlockAsync(other.Client, other.UserId, UkAt(_monday, 10), UkAt(_monday, 11))).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task SiteHours_CantShutOutStaffWorkingThere_NorCanTheSiteBeArchivedOrDeleted()
    {
        await GiveThemSaturdayMorningsAsync();

        var narrower = await PutSiteHoursAsync(Open("Monday", 10, 18), Open("Saturday", 8, 13));
        (await narrower.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain(_staff.Username);

        var archive = await _admin.PostAsync($"/api/sites/{_site.Id}/archive", null);
        (await archive.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("Change their hours first");
        await (await _admin.DeleteAsync($"/api/sites/{_site.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        // Staff hours outside the site's are refused too.
        (await (await PutStaffHoursAsync(_admin, Day("Saturday", 9, 15))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title
            .Should().Be(StaffHoursRules.OutsideSite(_site.Name, DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(13, 0)));
        (await (await PutStaffHoursAsync(_admin, Day("Sunday", 9, 12))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title
            .Should().Be(StaffHoursRules.SiteClosed(_site.Name, DayOfWeek.Sunday));

        // Back on the default, they no longer hold the site's hours.
        await (await PutStaffHoursAsync(_admin)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PutSiteHoursAsync(Open("Monday", 10, 18))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task Hours_KeepTheirHistory_ChangesApplyFromToday()
    {
        await GiveThemSaturdayMorningsAsync();
        Clock.SetUtcNow(Now.AddDays(3));

        await (await PutStaffHoursAsync(_admin, Day("Monday", 10, 16))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var rows = await RunInScopeAsync(async services =>
        {
            var context = services.GetRequiredService<MedAppDbContext>();
            return await context.StaffHours
                .AsNoTracking()
                .Where(h => h.StaffUserId == _staff.UserId)
                .OrderBy(h => h.ValidFrom).ThenBy(h => h.DayOfWeek)
                .Select(h => new { h.DayOfWeek, h.Starts, h.ValidFrom, h.ValidTo })
                .ToListAsync();
        });

        var today = UkToday;
        rows.Should().HaveCount(3);
        rows.Where(r => r.ValidTo == today.AddDays(-1)).Select(r => r.DayOfWeek).Should().BeEquivalentTo([DayOfWeek.Monday, DayOfWeek.Saturday]);
        rows.Should().ContainSingle(r => r.ValidTo == null).Which.Should().BeEquivalentTo(new { DayOfWeek = DayOfWeek.Monday, Starts = new TimeOnly(10, 0), ValidFrom = today });
    }
}
