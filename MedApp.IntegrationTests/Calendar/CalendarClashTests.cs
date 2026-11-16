using System.Net;
using FluentAssertions;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Nothing overlaps on a calendar: appointments, stored blocks and template blocks, checked in SQL Server's data with
/// the calendar lock held, including two bookings racing for one slot.
/// </summary>
[TestFixture]
public sealed class CalendarClashTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private PatientResponse _patient = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SetUpCalendarAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _patient = await CreatePatientAsync(_staff.Client);
        _day = NextWorkingDay();
    }

    [Test]
    public async Task CreateAppointment_OverlappingAnotherAppointment_Returns409WithoutNamingThePatient()
    {
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));

        var response = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10, 15));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Be("That time overlaps another appointment, 10:00 to 10:30.");
        problem.Title.Should().NotContain(_patient.LastName);
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Appointments.Should().ContainSingle();
    }

    [Test]
    public async Task CreateAppointment_TouchingAnotherEndToStart_IsBooked()
    {
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));

        var before = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 9, 30));
        var after = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10, 30));

        await before.ShouldHaveStatusAsync(HttpStatusCode.Created);
        await after.ShouldHaveStatusAsync(HttpStatusCode.Created);
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Appointments.Select(a => a.StartUtc).Should().BeEquivalentTo(
            [UkAt(_day, 9, 30), UkAt(_day, 10), UkAt(_day, 10, 30)]);
    }

    [Test]
    public async Task CreateAppointment_WithSomeoneElse_IsNotAClash()
    {
        var other = await CreateStaffAsync(UserRole);
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));

        await (await PostBookingAsync(_staff.Client, _patient.Id, other.UserId, UkAt(_day, 10)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateAppointment_CancelledAppointmentThere_IsBooked()
    {
        var cancelled = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));
        await (await PostCancelAsync(_staff.Client, cancelled.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateBlock_OverAnAppointment_Returns409()
    {
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));

        var response = await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 9), UkAt(_day, 11));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Be("That time overlaps another appointment, 10:00 to 10:30.");
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Blocks.Should().BeEmpty();
    }

    [Test]
    public async Task CreateAppointment_OverABlock_Returns409NamingTheActivity()
    {
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15));

        var response = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 14, 45));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Be("That time overlaps Training, 14:00 to 15:00.");
    }

    [Test]
    public async Task CreateBlock_OverAnotherBlock_Returns409()
    {
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15));

        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14, 30), UkAt(_day, 15, 30), LunchActivityTypeId))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 15), UkAt(_day, 16), LunchActivityTypeId))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateAppointment_OverATemplateBlock_Returns409()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));

        var response = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 12, 15));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Be("That time overlaps Lunch, 12:00 to 13:00.");
        await (await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 13)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateBlock_OverATemplateBlock_Returns409()
    {
        await GiveDefaultWeekAsync(_admin, _staff.UserId, TemplateBlock(12, 13));

        await (await PostBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 11), UkAt(_day, 12, 30)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task UpdateBlock_MovedIntoAClash_Returns409AndKeepsItWhereItWas()
    {
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));
        var block = await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15));

        var response = await PutBlockAsync(_staff.Client, block.BlockId!.Value, UkAt(_day, 10), UkAt(_day, 11));

        await response.ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        var stored = (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Blocks.Should().ContainSingle().Subject;
        stored.StartUtc.Should().Be(UkAt(_day, 14));
        // Moving it within its own time isn't a clash with itself.
        await (await PutBlockAsync(_staff.Client, block.BlockId!.Value, UkAt(_day, 14, 30), UkAt(_day, 15, 30)))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task BookAppointment_RescheduledIntoAClash_Returns409AndKeepsItsTime()
    {
        var appointment = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));
        await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 14), UkAt(_day, 15));

        await (await PostRescheduleAsync(_staff.Client, appointment.Id, _staff.UserId, UkAt(_day, 14, 30)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        (await GetAppointmentAsync(_staff.Client, appointment.Id)).StartUtc.Should().Be(UkAt(_day, 10));
        // Moving it later by a quarter of an hour overlaps only itself.
        var moved = await (await PostRescheduleAsync(_staff.Client, appointment.Id, _staff.UserId, UkAt(_day, 10, 15)))
            .ReadAsAsync<AppointmentResponse>();
        moved.StartUtc.Should().Be(UkAt(_day, 10, 15));
    }

    /// <summary>
    /// Two people book the same slot at the same moment, from two sessions. The calendar lock (sp_getapplock, inside the
    /// execution strategy's transaction) makes one wait for the other, which then sees the first booking and gets a 409.
    /// Repeated over a few slots, so a lucky ordering can't pass it by chance.
    /// </summary>
    [Test]
    public async Task CreateAppointment_TwoBookingsRacingForOneSlot_OneWinsAndTheOtherGets409()
    {
        var other = await CreateStaffAsync(UserRole);
        var target = await CreateStaffAsync(UserRole);
        var slots = Enumerable.Range(0, 4).Select(i => UkAt(_day, 9 + i)).ToList();

        foreach (var slot in slots)
        {
            var responses = await Task.WhenAll(
                PostBookingAsync(_staff.Client, _patient.Id, target.UserId, slot),
                PostBookingAsync(other.Client, _patient.Id, target.UserId, slot));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            (await loser.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("overlaps another appointment");
        }

        var calendar = await GetCalendarAsync(_staff.Client, target.UserId, _day);
        calendar.Appointments.Select(a => a.StartUtc).Should().BeEquivalentTo(slots.Cast<DateTime?>());
    }

    /// <summary>A booking and a block racing for the same time on one calendar: the same lock serialises them.</summary>
    [Test]
    public async Task BookingAndBlockRacing_ForOneSlot_OneWinsAndTheOtherGets409()
    {
        var slot = UkAt(_day, 15);

        var responses = await Task.WhenAll(
            PostBookingAsync(_admin, _patient.Id, _staff.UserId, slot),
            PostBlockAsync(_staff.Client, _staff.UserId, slot, slot.AddHours(1)));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, _day);
        (calendar.Appointments.Count + calendar.Blocks.Count).Should().Be(1);
    }
}
