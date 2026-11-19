using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Appointments;

/// <summary>
/// An appointment's statuses through the API, with time passing on the API's clock: requested, booked, attended (or
/// not, and corrected), cancelled for good; the booking rules; and who is told when someone else changes theirs.
/// </summary>
[TestFixture]
public sealed class AppointmentStatusTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private PatientResponse _patient = null!;

    [SetUp]
    public async Task SetUpPatientAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _patient = await CreatePatientAsync(_staff.Client);
    }

    [Test]
    public async Task Appointment_RequestedBookedAttendedThenCorrected_MovesThroughItsStatuses()
    {
        var requested = await RequestAppointmentAsync(_staff.Client, _patient.Id);
        requested.Should().BeEquivalentTo(new
        {
            Status = "Requested",
            StaffUserId = (Guid?)null,
            StartUtc = (DateTime?)null,
            CanBook = true,
            CanRecordOutcome = false,
            CreatedByUsername = _staff.Username,
        });

        var start = NextBookableStart();
        var booked = await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ReadAsAsync<AppointmentResponse>();
        booked.Should().BeEquivalentTo(new
        {
            Status = "Booked",
            StaffUserId = (Guid?)_staff.UserId,
            StaffUsername = _staff.Username,
            StartUtc = (DateTime?)start,
            EndUtc = (DateTime?)start.AddMinutes(AppointmentMinutes),
            DurationMinutes = (int?)AppointmentMinutes,
            BookedByUsername = _staff.Username,
            CanReschedule = true,
            CanRecordOutcome = false,
        });

        // Not before it starts.
        var early = await PostOutcomeAsync(_staff.Client, requested.Id, "Attended");
        (await early.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("once the appointment has started");

        MoveClockPast(start);
        var attended = await (await PostOutcomeAsync(_admin, requested.Id, "Attended")).ReadAsAsync<AppointmentResponse>();
        // Recorded by the test's clinic staff (admins are read only).
        attended.Should().BeEquivalentTo(new { Status = "Attended", OutcomeByUsername = (await ClinicStaffAsync()).Username, CanReschedule = false, CanCancel = false });

        var corrected = await (await PostOutcomeAsync(_staff.Client, requested.Id, "DidNotAttend")).ReadAsAsync<AppointmentResponse>();
        corrected.Should().BeEquivalentTo(new { Status = "DidNotAttend", OutcomeByUsername = _staff.Username });

        // A past appointment can't be moved.
        await (await PostRescheduleAsync(_staff.Client, requested.Id, _staff.UserId, NextBookableStart())).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task RecordOutcome_NotAnOutcome_Returns400()
    {
        var appointment = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());

        await (await PostOutcomeAsync(_staff.Client, appointment.Id, "Cancelled")).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RecordOutcome_RequestedAppointment_Returns409()
    {
        var requested = await RequestAppointmentAsync(_staff.Client, _patient.Id);

        await (await PostOutcomeAsync(_staff.Client, requested.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CancelAppointment_NeedsAReasonThenIsFinal()
    {
        var appointment = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());

        var noReason = await PostCancelAsync(_staff.Client, appointment.Id, " ");
        (await noReason.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("Reason");

        var cancelled = await (await PostCancelAsync(_staff.Client, appointment.Id, CancelReason)).ReadAsAsync<AppointmentResponse>();
        cancelled.Should().BeEquivalentTo(new { Status = "Cancelled", CancelReason, CanBook = false, CanCancel = false });

        await (await PostRescheduleAsync(_staff.Client, appointment.Id, _staff.UserId, NextBookableStart().AddHours(1)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await PostCancelAsync(_staff.Client, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await PostOutcomeAsync(_staff.Client, appointment.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        (await GetAppointmentAsync(_staff.Client, appointment.Id)).Status.Should().Be("Cancelled");
    }

    [Test]
    public async Task CreateAppointment_InThePast_Returns400()
    {
        var start = NextBookableStart();
        MoveClockPast(start);

        var response = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, start);

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(BookingTimes.InThePast);
    }

    [Test]
    public async Task BookAppointment_InThePast_Returns400()
    {
        var requested = await RequestAppointmentAsync(_staff.Client, _patient.Id);
        var start = NextBookableStart();
        MoveClockPast(start);

        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAppointment_OutsideWorkingHoursOrAtAWeekend_Returns400()
    {
        var day = NextWorkingDay();
        var saturday = day;
        while (saturday.DayOfWeek != DayOfWeek.Saturday)
            saturday = saturday.AddDays(1);

        var evening = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(day, 17, 45));
        var weekend = await PostBookingAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(saturday, 10));

        (await evening.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.OutsideWorkingDay);
        (await weekend.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Values.SelectMany(e => e)
            .Should().Contain(WorkingHours.NotWorking(DayOfWeek.Saturday));
    }

    [Test]
    public async Task CreateAppointment_OnlySomeOfStaffAndTimes_Returns400()
    {
        var response = await _staff.Client.PostAsJsonAsync("/api/appointments",
            new CreateAppointmentRequest { PatientId = _patient.Id, StaffUserId = _staff.UserId });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateAppointment_UnknownPatientOrStaff_Returns404()
    {
        await (await PostBookingAsync(_staff.Client, Guid.NewGuid(), _staff.UserId, NextBookableStart())).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await (await PostBookingAsync(_staff.Client, _patient.Id, Guid.NewGuid(), NextBookableStart())).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateNotes_SavesThemAndRecordsTheChange()
    {
        var appointment = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());

        var updated = await (await _staff.Client.PutAsJsonAsync($"{AppointmentUrl(appointment.Id)}/notes",
                new UpdateAppointmentNotesRequest { Notes = AppointmentNotes }))
            .ReadAsAsync<AppointmentResponse>();

        updated.Notes.Should().Be(AppointmentNotes);
        var timeline = await (await _admin.GetAsync($"/api/patients/{_patient.Id}/timeline?kinds=Appointments"))
            .ReadAsAsync<Contracts.Pathways.Timeline.Responses.PatientTimelinePageResponse>();
        timeline.Items.Select(e => e.Action).Should().Equal("AppointmentNotesUpdated", "AppointmentBooked");
    }

    [Test]
    public async Task SearchAppointments_ByStaffAndStatus_FiltersInSql()
    {
        var other = await CreateStaffAsync(UserRole);
        var mine = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());
        await BookNewAppointmentAsync(_staff.Client, _patient.Id, other.UserId, NextBookableStart());
        var requested = await RequestAppointmentAsync(_staff.Client, _patient.Id);

        (await SearchAppointmentsAsync(_staff.Client, $"staffUserId={_staff.UserId}")).Select(a => a.Id).Should().Equal(mine.Id);
        (await SearchAppointmentsAsync(_staff.Client, $"patientId={_patient.Id}&statuses=Requested")).Select(a => a.Id).Should().Equal(requested.Id);
        // Soonest first, Requested last.
        (await SearchAppointmentsAsync(_staff.Client, $"patientId={_patient.Id}")).Last().Id.Should().Be(requested.Id);
    }

    /// <summary>
    /// The member of staff is told when someone else books them, moves their appointment or cancels it; never about
    /// their own changes. Messages never carry the patient's name.
    /// </summary>
    [Test]
    public async Task Rescheduling_BySomeoneElse_NotifiesTheMemberOfStaffButNotWhenTheyDoItThemselves()
    {
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_admin, _patient.Id, _staff.UserId, start);
        (await NotificationsOfAsync(_staff.Client)).Items.Select(n => n.Kind).Should().Equal("AppointmentBooked");

        await (await PostRescheduleAsync(_admin, appointment.Id, _staff.UserId, start.AddHours(1))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var afterMove = await NotificationsOfAsync(_staff.Client);
        afterMove.Items.Select(n => n.Kind).Should().Equal("AppointmentRescheduled", "AppointmentBooked");
        afterMove.Items[0].Should().BeEquivalentTo(new
        {
            Title = "Appointment moved",
            AppointmentId = (Guid?)appointment.Id,
            IsRead = false,
        });
        afterMove.Items[0].Message.Should().Contain((await ClinicStaffAsync()).Username).And.Contain(UkTime.Describe(start.AddHours(1)));
        afterMove.Items.Should().AllSatisfy(n => n.Message.Should().NotContain(_patient.LastName));

        // Their own move: nothing new.
        await (await PostRescheduleAsync(_staff.Client, appointment.Id, _staff.UserId, start.AddHours(2))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await NotificationsOfAsync(_staff.Client)).Items.Should().HaveCount(2);
        (await NotificationsOfAsync(_admin)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task Rescheduling_ToSomeoneElse_TellsBothAndCancellingTellsTheNewOne()
    {
        var other = await CreateStaffAsync(UserRole);
        var appointment = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());
        (await NotificationsOfAsync(_staff.Client)).Items.Should().BeEmpty();

        await (await PostRescheduleAsync(_admin, appointment.Id, other.UserId, NextBookableStart())).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostCancelAsync(_admin, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        (await NotificationsOfAsync(_staff.Client)).Items.Select(n => n.Kind).Should().Equal("AppointmentCancelled");
        var theirs = await NotificationsOfAsync(other.Client);
        theirs.Items.Select(n => n.Kind).Should().Equal("AppointmentCancelled", "AppointmentBooked");
        theirs.Items.Should().AllSatisfy(n => n.Message.Should().NotContain(CancelReason));
    }

    private static async Task<NotificationsResponse> NotificationsOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
}
