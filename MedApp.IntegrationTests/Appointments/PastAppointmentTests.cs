using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Appointments.Requests;
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

    /// <summary>
    /// A procedure created early on the walk-in's day (07:00), so a visit that day isn't before it (a walk-in can't be
    /// before its procedure was created).
    /// </summary>
    private async Task<PatientProcedureResponse> CreateProcedureBeforeTheVisitAsync(Guid templateId)
    {
        Clock.SetUtcNow(UkAt(_day, 7));
        try
        {
            return await CreateProcedureAsync(_staff.Client, _patient.Id, templateId);
        }
        finally
        {
            Clock.Reset();
        }
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
    public async Task Record_OnADayClosedPartWayThrough_IsRecordedAndShowsOnTheClosedDay()
    {
        // Snow at 07:00: the day was closed while it was still to come; a walk-in seen that morning is recorded after.
        Clock.SetUtcNow(UkAt(_day, 7));
        await AddClosedDayAsync(_admin, _day);
        Clock.Reset();

        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        var calendar = await GetCalendarAsync(_staff.Client, _staff.UserId, _day);
        calendar.Days.Single().ClosedDayName.Should().Be(ClosedDayName);
        calendar.Appointments.Should().ContainSingle(a => a.Id == recorded.Id);
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
        var procedure = await CreateProcedureBeforeTheVisitAsync(template.Id);
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
        var procedure = await CreateProcedureBeforeTheVisitAsync(template.Id);
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

    // Entered in error

    private static Task<HttpResponseMessage> PostEnteredInErrorAsync(HttpClient client, Guid appointmentId, string reason = CancelReason) =>
        client.PostAsJsonAsync($"{AppointmentUrl(appointmentId)}/entered-in-error", new MarkEnteredInErrorRequest { Reason = reason });

    [Test]
    public async Task EnteredInError_ByWhoeverRecordedIt_TakesItOffTheCalendar_AndKeepsItInTheHistory()
    {
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);
        recorded.CanMarkEnteredInError.Should().BeTrue();

        var withdrawn = await (await PostEnteredInErrorAsync(_staff.Client, recorded.Id)).ReadAsAsync<EnteredInErrorResponse>();

        withdrawn.Appointment.Should().BeEquivalentTo(new { Status = "EnteredInError", CancelReason = CancelReason, CanMarkEnteredInError = false });
        (await GetCalendarAsync(_staff.Client, _staff.UserId, _day)).Appointments.Should().BeEmpty();
        var staffTimeline = await TimelineAsync(_staff.Client, _patient.Id);
        staffTimeline.Select(e => e.Action).Should().Equal("AppointmentEnteredInError", "AppointmentRecordedAfterEvent");
        staffTimeline[0].Reason.Should().BeNull("the reason is free text, for admins only");
        (await TimelineAsync(_admin, _patient.Id))[0].Reason.Should().Be(CancelReason);

        // The time is free again.
        await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Test]
    public async Task EnteredInError_BySomeoneElse_Returns403_ByAnAdmin_IsAllowed()
    {
        var other = await CreateStaffAsync(UserRole);
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10)))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        await (await PostEnteredInErrorAsync(other.Client, recorded.Id)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await PostEnteredInErrorAsync(_admin, recorded.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task EnteredInError_ABookedAppointment_Returns409()
    {
        var booked = await BookNewAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, NextBookableStart());

        await (await PostEnteredInErrorAsync(_admin, booked.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task EnteredInError_ARequiredStagesVisit_PutsItBackInRequiresAppointment()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Required");
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var procedure = await CreateProcedureBeforeTheVisitAsync(template.Id);
        var stage = procedure.Stages.Single();
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10),
                procedureId: procedure.Id, stageId: stage.Id))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);

        var withdrawn = await (await PostEnteredInErrorAsync(_staff.Client, recorded.Id)).ReadAsAsync<EnteredInErrorResponse>();

        withdrawn.VisitRequestedAgain.Should().BeTrue();
        withdrawn.StageMessage.Should().Contain("back in Requires Appointment");
        var appointments = await SearchAppointmentsAsync(_staff.Client, $"procedureId={procedure.Id}");
        appointments.Select(a => a.Status).Should().BeEquivalentTo(["EnteredInError", "Requested"]);
        // The stage can't be finished on the withdrawn visit.
        await (await FinishStageAsync(_staff.Client, procedure.Id, stage.Id)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task EnteredInError_AFinishedRequiredStagesOnlyVisit_TellsTheAssigneesAndAdmins_NotWhoWithdrewIt()
    {
        var definition = await CreateStageDefinitionAsync(_admin, visitRequirement: "Required");
        var template = await CreateTemplateAsync(_admin, definition.Id);
        var procedure = await CreateProcedureBeforeTheVisitAsync(template.Id);
        var stage = procedure.Stages.Single();
        var assignee = await CreateStaffAsync(UserRole);
        await (await _admin.PostAsync($"{ProcedureUrl(procedure.Id)}/assignments/{assignee.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var recorded = await (await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10),
                procedureId: procedure.Id, stageId: stage.Id))
            .ReadAsAsync<AppointmentResponse>(HttpStatusCode.Created);
        await (await FinishStageAsync(_staff.Client, procedure.Id, stage.Id)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        var withdrawn = await (await PostEnteredInErrorAsync(_staff.Client, recorded.Id)).ReadAsAsync<EnteredInErrorResponse>();

        withdrawn.VisitRequestedAgain.Should().BeFalse();
        withdrawn.StageMessage.Should().Contain("is finished, and now has no attended visit").And.Contain("assignees and the admins are told");
        foreach (var told in new[] { assignee.Client, _admin })
        {
            var notification = (await (await told.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>())
                .Items.Should().ContainSingle(n => n.Kind == "StageVisitWithdrawn").Subject;
            notification.Title.Should().Be("Stage visit withdrawn");
            notification.ProcedureId.Should().Be(procedure.Id);
            notification.AppointmentId.Should().BeNull();
            notification.Message.Should().StartWith($"{_staff.Username} marked the visit on ")
                .And.Contain(stage.Name).And.NotContain(_patient.LastName).And.NotContain(procedure.Title).And.NotContain(CancelReason);
        }
        (await (await _staff.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>())
            .Items.Should().NotContain(n => n.Kind == "StageVisitWithdrawn");
    }

    // The procedure and the stage

    [Test]
    public async Task Record_BeforeTheProcedureWasCreated_Returns409()
    {
        var template = await CreateTemplateWithStagesAsync(_admin, 1);
        var procedure = await CreateProcedureAsync(_staff.Client, _patient.Id, template.Id);

        var response = await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10), procedureId: procedure.Id);

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().StartWith("This visit was before the day the procedure was created");
    }

    [Test]
    public async Task Record_ForAStageNotReachedYet_Returns409()
    {
        // The procedure was created on the walk-in's day, before it, so only the stage is wrong.
        Clock.SetUtcNow(UkAt(_day, 8));
        var template = await CreateTemplateWithStagesAsync(_admin, 2);
        var procedure = await CreateProcedureAsync(_staff.Client, _patient.Id, template.Id);
        Clock.Reset();

        var response = await PostPastAppointmentAsync(_staff.Client, _patient.Id, _staff.UserId, UkAt(_day, 10),
            procedureId: procedure.Id, stageId: procedure.Stages[1].Id);

        (await response.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("hasn't been reached yet");
    }
}
