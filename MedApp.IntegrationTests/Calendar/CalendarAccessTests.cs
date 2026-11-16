using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Who may change what: a calendar's owner or an admin changes its blocks, days, default week and day and week templates
/// (403 for other staff, from the handlers' ForbiddenException through the middleware); admins manage activity types.
/// Also the working-day rules (400) and the seeded activity types surviving the reset between tests.
/// </summary>
[TestFixture]
public sealed class CalendarAccessTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private StaffAccount _other = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _other = await CreateStaffAsync(UserRole);
        _day = NextWorkingDay();
    }

    [Test]
    public async Task Blocks_SomeoneElsesAsStaff_Return403AndAsAdminSucceed()
    {
        var block = await CreateBlockAsync(_other.Client, _other.UserId, UkAt(_day, 9), UkAt(_day, 10));

        var create = await PostBlockAsync(_staff.Client, _other.UserId, UkAt(_day, 11), UkAt(_day, 12));
        var move = await PutBlockAsync(_staff.Client, block.BlockId!.Value, UkAt(_day, 14), UkAt(_day, 15));
        var delete = await _staff.Client.DeleteAsync($"/api/calendar/blocks/{block.BlockId}");

        (await create.ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden)).Title.Should().Be(CalendarActor.NotYourCalendar);
        await move.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await delete.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await GetCalendarAsync(_staff.Client, _other.UserId, _day)).Blocks.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { StartUtc = UkAt(_day, 9), CanEdit = false });

        await (await PostBlockAsync(_admin, _other.UserId, UkAt(_day, 11), UkAt(_day, 12))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await PutBlockAsync(_admin, block.BlockId!.Value, UkAt(_day, 14), UkAt(_day, 15))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.DeleteAsync($"/api/calendar/blocks/{block.BlockId}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DaysAndDefaultWeek_SomeoneElsesAsStaff_Return403()
    {
        var day = await CreateDayTemplateAsync(_admin, _other.UserId, TemplateBlock(12, 13));
        var week = await CreateWeekTemplateAsync(_admin, _other.UserId, day.Id);
        var dayUrl = $"/api/calendar/staff/{_other.UserId}/days/{_day:yyyy-MM-dd}/day-template";

        await (await SetDefaultWeekAsync(_staff.Client, _other.UserId, week.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PutAsJsonAsync(dayUrl, new ApplyDayTemplateRequest { DayTemplateId = day.Id }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.DeleteAsync(dayUrl)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);

        // Their own, and an admin for anyone.
        await (await SetDefaultWeekAsync(_staff.Client, _staff.UserId, week.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await SetDefaultWeekAsync(_admin, _other.UserId, week.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var staffList = await (await _staff.Client.GetAsync("/api/calendar/staff")).ReadAsAsync<List<StaffMemberResponse>>();
        staffList.Where(s => s.UserId == _staff.UserId || s.UserId == _other.UserId)
            .Should().HaveCount(2).And.AllSatisfy(s => s.WeekTemplateId.Should().Be(week.Id));
    }

    [Test]
    public async Task Appointments_ForSomeoneElseAsStaff_CanBeBookedMovedAndCancelled()
    {
        var patient = await CreatePatientAsync(_staff.Client);

        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _other.UserId, UkAt(_day, 9));
        await (await PostRescheduleAsync(_staff.Client, appointment.Id, _other.UserId, UkAt(_day, 11))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostCancelAsync(_staff.Client, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        appointment.StaffUsername.Should().Be(_other.Username);
        appointment.BookedByUsername.Should().Be(_staff.Username);
    }

    [Test]
    public async Task GetCalendar_AnyonesAsStaff_ShowsItButCanEditOnlyTheirOwn()
    {
        var mine = await GetCalendarAsync(_staff.Client, _staff.UserId, _day);
        var theirs = await GetCalendarAsync(_staff.Client, _other.UserId, _day);
        var asAdmin = await GetCalendarAsync(_admin, _other.UserId, _day);

        mine.CanEdit.Should().BeTrue();
        theirs.Should().BeEquivalentTo(new { CanEdit = false, StaffUsername = _other.Username });
        asAdmin.CanEdit.Should().BeTrue();
    }

    [Test]
    public async Task GetCalendar_NoStaffUserId_IsYourOwn()
    {
        var calendar = await (await _staff.Client.GetAsync($"/api/calendar?from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}"))
            .ReadAsAsync<CalendarResponse>();

        calendar.StaffUserId.Should().Be(_staff.UserId);
    }

    [Test]
    public async Task GetCalendar_MoreThan31Days_Returns400()
    {
        var response = await _staff.Client.GetAsync($"/api/calendar?from={_day:yyyy-MM-dd}&to={_day.AddDays(31):yyyy-MM-dd}");

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateBlock_OutsideTheWorkingDayOrAtAWeekend_Returns400()
    {
        var saturday = _day;
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
            saturday = saturday.AddDays(1);

        var early = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 7), UkAt(_day, 9));
        var weekend = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(saturday, 10), UkAt(saturday, 11));
        var offBoundary = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 10, 2), UkAt(_day, 11));
        var backwards = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 11), UkAt(_day, 10));

        (await early.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.OutsideWorkingDay);
        (await weekend.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.Weekend);
        (await offBoundary.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.NotOnBoundary);
        (await backwards.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.EndNotAfterStart);
    }

    [Test]
    public async Task ActivityTypes_WritesAsStaff_Return403AndReadsSucceed()
    {
        var client = _staff.Client;
        var activityType = await CreateActivityTypeAsync(_admin);

        var writes = new[]
        {
            await client.PostAsJsonAsync("/api/calendar/activity-types", new CreateActivityTypeRequest { Name = UniqueName("Activity"), Colour = ActivityColour }),
            await client.PatchAsJsonAsync($"/api/calendar/activity-types/{activityType.Id}", new UpdateActivityTypeRequest { Name = UniqueName("Renamed") }),
            await client.PostAsync($"/api/calendar/activity-types/{activityType.Id}/archive", null),
            await client.DeleteAsync($"/api/calendar/activity-types/{activityType.Id}"),
        };

        writes.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Forbidden));
        (await GetActivityTypesAsync(client)).Should().Contain(t => t.Id == activityType.Id && t.Name == activityType.Name);
    }

    /// <summary>
    /// Templates are personal: another member of staff can read someone's (the calendar names the template a day came
    /// from) but not add to, change, archive or delete them (403); their owner and an admin can.
    /// </summary>
    [Test]
    public async Task Templates_SomeoneElsesAsStaff_Return403AndReadsSucceed()
    {
        var client = _staff.Client;
        var day = await CreateDayTemplateAsync(_other.Client, _other.UserId, TemplateBlock(12, 13));
        var week = await CreateWeekTemplateAsync(_other.Client, _other.UserId, day.Id);

        var writes = new[]
        {
            await PostDayTemplateAsync(client, _other.UserId, UniqueName("Day"), TemplateBlock(12, 13)),
            await client.PutAsJsonAsync($"/api/calendar/day-templates/{day.Id}", new DayTemplateRequest { Name = day.Name, Blocks = [] }),
            await client.PostAsync($"/api/calendar/day-templates/{day.Id}/archive", null),
            await client.DeleteAsync($"/api/calendar/day-templates/{day.Id}"),
            await PostWeekTemplateAsync(client, _other.UserId, null),
            await client.PutAsJsonAsync($"/api/calendar/week-templates/{week.Id}", new WeekTemplateRequest { Name = week.Name }),
            await client.PostAsync($"/api/calendar/week-templates/{week.Id}/archive", null),
            await client.DeleteAsync($"/api/calendar/week-templates/{week.Id}"),
        };

        writes.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Forbidden));
        (await writes[0].ReadAsAsync<ProblemDetails>(HttpStatusCode.Forbidden)).Title.Should().Be(CalendarActor.NotYourTemplates);
        (await (await client.GetAsync($"/api/calendar/day-templates/{day.Id}")).ReadAsAsync<DayTemplateResponse>())
            .Should().BeEquivalentTo(new { OwnerUserId = _other.UserId, CanEdit = false });
        (await (await client.GetAsync($"/api/calendar/staff/{_other.UserId}/week-templates")).ReadAsAsync<List<WeekTemplateResponse>>())
            .Should().ContainSingle().Which.Should().BeEquivalentTo(new { week.Id, CanEdit = false });

        // The admin can, for support.
        await (await _admin.PutAsJsonAsync($"/api/calendar/day-templates/{day.Id}", new DayTemplateRequest { Name = day.Name, Blocks = [] }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await (await _admin.GetAsync($"/api/calendar/staff/{_other.UserId}/day-templates")).ReadAsAsync<List<DayTemplateResponse>>())
            .Should().ContainSingle().Which.CanEdit.Should().BeTrue();
    }

    /// <summary>
    /// A person's templates use only their own: a week template can't name someone else's day template, and a day
    /// override or a default week can't be someone else's (404, as if it weren't there). Names are unique per owner.
    /// </summary>
    [Test]
    public async Task Templates_SomeoneElsesTemplateInMine_Return404AndNamesArePerOwner()
    {
        var theirs = await CreateDayTemplateAsync(_other.Client, _other.UserId, TemplateBlock(12, 13));
        var theirWeek = await CreateWeekTemplateAsync(_other.Client, _other.UserId, theirs.Id);

        await (await PostWeekTemplateAsync(_staff.Client, _staff.UserId, theirs.Id)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await _staff.Client.PutAsJsonAsync(
                $"/api/calendar/staff/{_staff.UserId}/days/{_day:yyyy-MM-dd}/day-template",
                new ApplyDayTemplateRequest { DayTemplateId = theirs.Id }))
            .ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await SetDefaultWeekAsync(_staff.Client, _staff.UserId, theirWeek.Id)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        // The same name is fine for someone else, but not twice for one person.
        await (await PostDayTemplateAsync(_staff.Client, _staff.UserId, theirs.Name, TemplateBlock(12, 13)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await PostDayTemplateAsync(_staff.Client, _staff.UserId, theirs.Name, TemplateBlock(12, 13)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ActivityType_UsedByABlock_IsArchivedNotDeletedAndCantBeChosen()
    {
        var activityType = await CreateActivityTypeAsync(_admin);
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 9), UkAt(_day, 10), activityType.Id);

        await (await _admin.DeleteAsync($"/api/calendar/activity-types/{activityType.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PostAsync($"/api/calendar/activity-types/{activityType.Id}/archive", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 11), UkAt(_day, 12), activityType.Id))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        // The existing block keeps it.
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Blocks.Should().ContainSingle()
            .Which.ActivityName.Should().Be(activityType.Name);
    }

    /// <summary>
    /// The reset between tests keeps the six seeded activity types, puts them back as the migration made them (even
    /// renamed, swapped round or archived), and removes any other, including ones blocks and templates used.
    /// </summary>
    [Test]
    public async Task ResetDatabase_AfterActivityTypesChanged_PutsBackTheSeededSix()
    {
        var added = await CreateActivityTypeAsync(_admin);
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 9), UkAt(_day, 10), added.Id);
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13, added.Id));
        var travelId = Guid.Parse("22222222-0000-0000-0000-000000000006");
        await (await _admin.PatchAsJsonAsync($"/api/calendar/activity-types/{LunchActivityTypeId}", new UpdateActivityTypeRequest { Name = UniqueName("Renamed"), Colour = "pink" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PatchAsJsonAsync($"/api/calendar/activity-types/{travelId}", new UpdateActivityTypeRequest { Name = "Lunch" }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PostAsync($"/api/calendar/activity-types/{TrainingActivityTypeId}/archive", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await ResetAsync();

        var types = await GetActivityTypesAsync(await SignInAsAdminAsync());
        types.Select(t => t.Name).Should().BeEquivalentTo(SeededActivityTypeNames);
        types.Should().AllSatisfy(t => t.IsArchived.Should().BeFalse());
        types.Single(t => t.Id == LunchActivityTypeId).Should().BeEquivalentTo(new { Name = "Lunch", Colour = "green" });
        types.Single(t => t.Id == travelId).Name.Should().Be("Travel");
    }
}
