using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Closed days: admins keep the list (one per date, past days unchanged), anyone reads it, and a closed day is like a
/// weekend for everything new (bookings, blocks, meetings and templates refused with a 400, free times skipping it),
/// while what was already there stays and shows.
/// </summary>
[TestFixture]
public sealed class ClosedDayTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private PatientResponse _patient = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _patient = await CreatePatientAsync(_staff.Client);
        _day = NextWorkingDay(2);
    }

    private static async Task<List<ClosedDayResponse>> ListAsync(HttpClient client, DateOnly from, DateOnly to) =>
        await (await client.GetAsync($"/api/calendar/closed-days?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"))
            .ReadAsAsync<List<ClosedDayResponse>>();

    private string ClosedMessage => WorkingHours.ClosedDay(_day, ClosedDayName);

    [Test]
    public async Task Add_AsAdmin_StaffSeeItInTheList()
    {
        var added = await AddClosedDayAsync(_admin, _day);

        added.Should().BeEquivalentTo(new { Date = _day, Name = ClosedDayName, IsPast = false });
        (await ListAsync(_staff.Client, _day, _day)).Should().ContainSingle().Which.Id.Should().Be(added.Id);
        (await ListAsync(_staff.Client, _day.AddDays(1), _day.AddDays(7))).Should().BeEmpty();
    }

    [Test]
    public async Task Writes_AsStaff_Return403()
    {
        var added = await AddClosedDayAsync(_admin, _day);

        await (await PostClosedDayAsync(_staff.Client, _day.AddDays(1))).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.DeleteAsync($"/api/calendar/closed-days/{added.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PostAsJsonAsync("/api/calendar/closed-days/bank-holidays", new AddBankHolidaysRequest { Year = 2030 }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Add_TheSameDateTwice_Returns409()
    {
        await AddClosedDayAsync(_admin, _day);

        var again = await PostClosedDayAsync(_admin, _day, "Another name");

        (await again.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().EndWith($"is already a closed day: {ClosedDayName}.");
    }

    [Test]
    public async Task Add_PastDay_Returns409()
    {
        var response = await PostClosedDayAsync(_admin, UkToday.AddDays(-1));

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(ClosedDayRules.PastCantBeAdded);
    }

    [Test]
    public async Task Add_NoName_Returns400()
    {
        var response = await PostClosedDayAsync(_admin, _day, " ");

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("Name");
    }

    [Test]
    public async Task Remove_FutureDay_OpensItAgain()
    {
        var added = await AddClosedDayAsync(_admin, _day);

        await (await _admin.DeleteAsync($"/api/calendar/closed-days/{added.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await ListAsync(_admin, _day, _day)).Should().BeEmpty();
        await (await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task Remove_OnceItHasPassed_Returns409()
    {
        var added = await AddClosedDayAsync(_admin, _day);
        MoveClockPast(UkAt(_day.AddDays(1), 9));

        var response = await _admin.DeleteAsync($"/api/calendar/closed-days/{added.Id}");

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(ClosedDayRules.PastCantBeRemoved);
        (await ListAsync(_admin, _day, _day)).Single().IsPast.Should().BeTrue();
    }

    [Test]
    public async Task Remove_Unknown_Returns404()
    {
        await (await _admin.DeleteAsync($"/api/calendar/closed-days/{Guid.NewGuid()}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task NewThings_OnAClosedDay_Return400FromTheWorkingDayRule()
    {
        await AddClosedDayAsync(_admin, _day);
        var requested = await RequestAppointmentAsync(_staff.Client, _patient.Id);
        var template = await CreateDayTemplateAsync(_staff.Client, _staff.UserId, TemplateBlock(12, 13));

        var responses = new[]
        {
            await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)),
            await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, UkAt(_day, 10)),
            await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15)),
            await _admin.PostAsJsonAsync("/api/calendar/meetings", new MeetingRequest
            {
                Title = UniqueName("Meeting"),
                StartUtc = UkAt(_day, 16),
                EndUtc = UkAt(_day, 17),
                AttendeeUserIds = [_staff.UserId],
            }),
            await _staff.Client.PutAsJsonAsync($"/api/calendar/staff/{_staff.UserId}/days/{_day:yyyy-MM-dd}/day-template",
                new ApplyDayTemplateRequest { DayTemplateId = template.Id }),
        };

        foreach (var response in responses)
        {
            var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
            problem.Errors.SelectMany(e => e.Value).Should().Contain(ClosedMessage);
        }
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Appointments.Should().BeEmpty();
    }

    [Test]
    public async Task Calendar_ClosedDay_IsNotWorkingAndNamed_TemplatesDontApply_AndWhatWasThereStays()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));
        var booked = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));
        var block = await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15));

        await AddClosedDayAsync(_admin, _day);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, _day, _day.AddDays(1));
        var closed = calendar.Days.Single(d => d.Date == _day);
        closed.IsWorkingDay.Should().BeFalse();
        closed.ClosedDayName.Should().Be(ClosedDayName);
        closed.DayTemplateId.Should().BeNull();
        calendar.Appointments.Should().ContainSingle(a => a.Id == booked.Id && a.Status == "Booked", "the app never moves or cancels it");
        calendar.Blocks.Should().ContainSingle(b => b.Date == _day).Which.BlockId.Should().Be(block.BlockId);
        calendar.Blocks.Where(b => b.IsFromTemplate).Select(b => b.Date).Should().NotContain(_day);

        var next = calendar.Days.Single(d => d.Date == _day.AddDays(1));
        if (next.IsWorkingDay)
            calendar.Blocks.Should().Contain(b => b.IsFromTemplate && b.Date == next.Date, "templates still apply on open days");
    }

    [Test]
    public async Task Availability_SkipsAClosedDay()
    {
        await AddClosedDayAsync(_admin, _day);

        var availability = await (await _admin.GetAsync(
                $"/api/calendar/availability?staffUserIds={_staff.UserId}&from={_day:yyyy-MM-dd}&to={_day.AddDays(1):yyyy-MM-dd}&durationMinutes=30"))
            .ReadAsAsync<AvailabilityResponse>();

        availability.Days.Select(d => d.Date).Should().NotContain(_day);
        availability.Suggestions.Should().NotContain(s => UkTime.DateOf(s.StartUtc) == _day);
    }

    [Test]
    public async Task BankHolidays_ForAYearAhead_AddsEight_ThenSkipsThemAll()
    {
        var year = UkToday.Year + 2;

        var first = await (await _admin.PostAsJsonAsync("/api/calendar/closed-days/bank-holidays", new AddBankHolidaysRequest { Year = year }))
            .ReadAsAsync<AddBankHolidaysResponse>();
        var second = await (await _admin.PostAsJsonAsync("/api/calendar/closed-days/bank-holidays", new AddBankHolidaysRequest { Year = year }))
            .ReadAsAsync<AddBankHolidaysResponse>();

        first.Added.Select(d => d.Date).Should().Equal(BankHolidays.EnglandAndWales(year).Select(h => h.Date));
        first.Skipped.Should().BeEmpty();
        second.Added.Should().BeEmpty();
        second.Skipped.Should().HaveCount(8).And.OnlyContain(s => s.Reason == "AlreadyClosed");
        (await ListAsync(_staff.Client, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31))).Should().HaveCount(8);
    }

    [Test]
    public async Task BankHolidays_YearOutOfRange_Returns400()
    {
        var response = await _admin.PostAsJsonAsync("/api/calendar/closed-days/bank-holidays", new AddBankHolidaysRequest { Year = 1999 });

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("Year");
    }
}
