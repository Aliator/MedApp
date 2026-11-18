using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Appointments;

/// <summary>
/// Appointments recorded after the event (walk-ins): the time rules, the clash with other appointments (but not with
/// blocked time), a stage's visit in Requires Appointment becoming the recorded one so a Required stage can be finished, the
/// restriction guard, the history and timeline entry, and the notification to the member of staff.
/// </summary>
[TestFixture]
public sealed class PastAppointmentTests : IntegrationTest
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
        _day = PreviousWorkingDay();
    }

    private static async Task<List<PatientTimelineEntryResponse>> TimelineAsync(HttpClient client, Guid patientId) =>
        (await (await client.GetAsync($"/api/patients/{patientId}/timeline?kinds=Appointments&pageSize=100"))
            .ReadAsAsync<PatientTimelinePageResponse>()).Items;

    [Test]
    public async Task Record_AWalkIn_IsAttendedOnTheCalendarWithItsHistory()
    {
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10), notes: AppointmentNotes))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        recorded.Should().BeEquivalentTo(new
        {
            Status = "Attended",
            IsRecordedAfterEvent = true,
            StaffUserId = (Guid?)_staff.UserId,
            StartUtc = (DateTime?)UkAt(_day, 10),
            Notes = AppointmentNotes,
            BookedByUsername = _staff.Username,
            OutcomeByUsername = _staff.Username,
        });
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Appointments.Should().ContainSingle(a => a.Id == recorded.Id);

        var entry = (await TimelineAsync(_staff.Client, _patient.Id)).Should().ContainSingle().Subject;
        entry.Should().BeEquivalentTo(new
        {
            Action = "AppointmentRecordedAfterEvent",
            ToStatus = "Attended",
            FromStatus = (string?)null,
            Username = _staff.Username,
            AppointmentStaffUsername = _staff.Username,
        });
    }

    [Test]
    public async Task Record_DidNotAttend_KeepsThatOutcome()
    {
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 11), "DidNotAttend"))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        recorded.Status.Should().Be("DidNotAttend");
        recorded.CanRecordOutcome.Should().BeTrue("the outcome can still be corrected");
    }

    [Test]
    public async Task Record_OverAnotherAppointment_Returns409WithoutNamingThePatient()
    {
        await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var response = await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10, 15));

        var problem = await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        problem.Title.Should().Be("That time overlaps another appointment, 10:00 to 10:30.");
        problem.Title.Should().NotContain(_patient.LastName);
    }

    [Test]
    public async Task Record_OverBlockedTime_IsRecordedAndTheBlockFlagged()
    {
        // The block was put there while the day was still to come.
        Clock.SetUtcNow(UkAt(_day, 7));
        var block = await CreateBlockAsync(_staff.Client, _staff.UserId, UkAt(_day, 9), UkAt(_day, 12));
        Clock.Reset();

        await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.Created);

        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Blocks.Single(b => b.BlockId == block.BlockId)
            .OverlapsAppointment.Should().BeTrue();
    }

    [Test]
    public async Task Record_TimesTheRulesRefuse_Return400()
    {
        var tooLongAgo = UkToday.AddDays(-(BookingTimes.RecordAfterEventDays + 7));
        while (!WorkingHours.IsWorkingDay(tooLongAgo))
            tooLongAgo = tooLongAgo.AddDays(-1);

        var cases = new[]
        {
            (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart()), BookingTimes.NotStartedYet),
            (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(tooLongAgo, 10)), BookingTimes.TooLongAgo),
            (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 7)), WorkingHours.OutsideWorkingDay),
        };

        foreach (var (response, message) in cases)
            (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
                .Errors.SelectMany(e => e.Value).Should().Contain(message);
    }

    [Test]
    public async Task Record_OnAClosedDay_Returns400()
    {
        // Closed while it was still to come, then recorded after it.
        Clock.SetUtcNow(UkAt(_day, 7));
        await AddClosedDayAsync(_admin, _day);
        Clock.Reset();

        var response = await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10));

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest))
            .Errors.SelectMany(e => e.Value).Should().Contain(WorkingHours.ClosedDay(_day, ClosedDayName));
    }

    [Test]
    public async Task Record_ForARestrictedPatient_Returns409()
    {
        await (await _admin.PutAsJsonAsync($"/api/patients/{_patient.Id}/restriction",
                new SetPatientFlagRequest { Set = true, Reason = "Synthetic restriction", ConfirmOpenProcedures = true }))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Record_RequiredStagesVisit_TheNeedsBookingOneBecomesItAndTheStageCanBeFinished()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Required");
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var procedure = await CreateProcedureAsync(_staff.Client, _patient.Id, template.Id);
        var stage = procedure.Stages.Single();
        var requested = (await SearchAppointmentsAsync(_staff.Client, $"procedureId={procedure.Id}")).Single();
        requested.Status.Should().Be("Requested");

        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10),
                procedureId: procedure.Id, stageId: stage.Id))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        recorded.Id.Should().Be(requested.Id, "the stage's visit in Requires Appointment is the one recorded");
        (await SearchAppointmentsAsync(_staff.Client, $"procedureId={procedure.Id}")).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Status = "Attended", IsRecordedAfterEvent = true });
        (await TimelineAsync(_staff.Client, _patient.Id)).Select(e => (e.Action, e.FromStatus))
            .Should().Equal(("AppointmentRecordedAfterEvent", "Requested"), ("AppointmentRequested", null));

        var finished = await (await FinishStageAsync(_staff.Client, procedure.Id, stage.Id)).ReadAsAsync<PatientProcedureResponse>();
        finished.Stages.Single().State.Should().Be("Completed");
    }

    [Test]
    public async Task Record_ForAStageWithAVisitBooked_Returns409SayingToRecordThatOne()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Suggested");
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var procedure = await CreateProcedureAsync(_staff.Client, _patient.Id, template.Id);
        var stage = procedure.Stages.Single();
        var requested = (await SearchAppointmentsAsync(_staff.Client, $"procedureId={procedure.Id}")).Single();
        await (await PostBookAsync(_staff.Client, requested.Id, _staff.UserId, NextBookableStart())).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var response = await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10),
            procedureId: procedure.Id, stageId: stage.Id);

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("record its outcome instead");
    }

    [Test]
    public async Task Record_BySomeoneElse_TellsTheStaffMemberWithoutThePatient()
    {
        await (await PostPastAppointmentAsync(_admin, _patient.Id, _staff.UserId, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.Created);

        var notification = (await (await _staff.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>())
            .Items.Should().ContainSingle().Subject;
        notification.Kind.Should().Be("AppointmentRecorded");
        notification.Title.Should().Be("Appointment recorded");
        notification.Message.Should().StartWith($"{AdminUsername} recorded an appointment you had on ").And.NotContain(_patient.LastName);
    }
}
