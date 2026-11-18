using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Procedures.Timeline.Common;

[TestFixture]
public sealed class TimelineMapperTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ToResponse_Entry_MapsWhenWhoWhatAndTheProcedureNow(bool isAdmin)
    {
        var entry = CreateEntry(Action.StageFinished, Now, stageName: ConsultationStage);

        var response = TimelineMapper.ToResponse(new PatientTimelineRow(entry, CurrentTitle), isAdmin);

        response.Should().BeEquivalentTo(new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = "Stages",
            Action = "StageFinished",
            AtUtc = Now,
            Username = Staff.Username,
            ProcedureId = ProcedureId,
            ProcedureTitle = CurrentTitle,
            IsProcedureDeleted = false,
            StageName = ConsultationStage,
        });
    }

    [Test]
    public void ToResponse_DeletedProcedure_UsesTheTitleAtTheTimeAndSaysSo()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(CreateEntry(Action.Created, Now, procedureTitle: DeletedTitle), null), true);

        response.ProcedureTitle.Should().Be(DeletedTitle);
        response.IsProcedureDeleted.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ToResponse_Created_ReadsTheTemplateStagesPriorityAndDueDate(bool isAdmin)
    {
        var details = Audited(change => change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 }));

        var response = Map(details, isAdmin);

        response.TemplateName.Should().Be(TemplateName);
        response.StageCount.Should().Be(3);
        response.Priority.Should().Be("High");
        response.DueDate.Should().Be(Today);
    }

    [Test]
    public void ToResponse_CreatedWithoutDetails_LeavesThemOut()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(CreateEntry(Action.Created, Now, "{\"stageCount\":\"three\"}"), CurrentTitle), true);

        response.TemplateName.Should().BeNull();
        response.StageCount.Should().BeNull();
        response.Priority.Should().BeNull();
        response.DueDate.Should().BeNull();
    }

    [Test]
    public void ToResponse_StatusChangeForAnAdmin_IncludesTheReason()
    {
        var details = Audited(change => change.ChangeStatus(PatientProcedureStatus.Blocked, Reason));

        var response = Map(details, isAdmin: true);

        response.Kind.Should().Be("Blocks");
        response.FromStatus.Should().Be("InProgress");
        response.ToStatus.Should().Be("Blocked");
        response.IsAutomatic.Should().BeFalse();
        response.Reason.Should().Be(Reason);
    }

    [Test]
    public void ToResponse_StatusChangeForStaff_LeavesTheReasonOut()
    {
        var details = Audited(change => change.ChangeStatus(PatientProcedureStatus.Blocked, Reason));

        var response = Map(details, isAdmin: false);

        response.FromStatus.Should().Be("InProgress");
        response.ToStatus.Should().Be("Blocked");
        response.Reason.Should().BeNull();
    }

    [Test]
    public void ToResponse_AutomaticStatusChange_SaysSo()
    {
        var response = Map(CreateEntry(Action.StatusChanged, Now, "{\"from\":\"InProgress\",\"to\":\"InReview\",\"automatic\":true}"), false);

        response.IsAutomatic.Should().BeTrue();
        response.Kind.Should().Be("Status");
    }

    [TestCase(Action.Assigned)]
    [TestCase(Action.Unassigned)]
    public void ToResponse_Assignment_NamesWho(Action action)
    {
        var response = Map(CreateEntry(action, Now, $"{{\"userId\":\"{OtherUserId}\",\"username\":\"{OtherUsername}\"}}"), false);

        response.AssigneeUsername.Should().Be(OtherUsername);
        response.Kind.Should().Be("Assignments");
    }

    [Test]
    public void ToResponse_StageNotesForAnAdmin_IncludesTheText()
    {
        var response = Map(CreateEntry(Action.StageNotesUpdated, Now, $"{{\"from\":\"\",\"to\":\"{NotesText}\"}}"), true);

        response.Changes.Should().BeEquivalentTo([new PatientTimelineChangeResponse { Field = "stageNotes", From = "", To = NotesText }]);
    }

    [Test]
    public void ToResponse_StageNotesForStaff_SaysOnlyThatTheyChanged()
    {
        var response = Map(CreateEntry(Action.StageNotesUpdated, Now, $"{{\"from\":\"\",\"to\":\"{NotesText}\"}}"), false);

        response.Changes.Should().BeEquivalentTo([new PatientTimelineChangeResponse { Field = "stageNotes" }]);
    }

    [Test]
    public void ToResponse_DetailsForAnAdmin_IncludeEveryValue()
    {
        var response = Map(EditedDetails(), isAdmin: true);

        response.Kind.Should().Be("DueDates");
        response.Changes.Should().BeEquivalentTo(
        [
            new PatientTimelineChangeResponse { Field = "title", From = ProcedureTitle, To = CurrentTitle },
            new PatientTimelineChangeResponse { Field = "notes", From = "", To = NotesText },
            new PatientTimelineChangeResponse { Field = "dueDate", From = "2026-06-15", To = "2026-06-16" },
            new PatientTimelineChangeResponse { Field = "priority", From = "Medium", To = "High" },
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void ToResponse_DetailsForStaff_IncludeOnlyDueDateAndPriorityValues()
    {
        var response = Map(EditedDetails(), isAdmin: false);

        response.Changes.Should().BeEquivalentTo(
        [
            new PatientTimelineChangeResponse { Field = "title" },
            new PatientTimelineChangeResponse { Field = "notes" },
            new PatientTimelineChangeResponse { Field = "dueDate", From = "2026-06-15", To = "2026-06-16" },
            new PatientTimelineChangeResponse { Field = "priority", From = "Medium", To = "High" },
        ], options => options.WithStrictOrdering());
    }

    [Test]
    public void ToResponse_DetailsChangeThatIsNotAnObject_HasNoValues()
    {
        var response = Map(CreateEntry(Action.DetailsUpdated, Now, "{\"changes\":{\"priority\":42}}"), true);

        response.Changes.Should().BeEquivalentTo([new PatientTimelineChangeResponse { Field = "priority" }]);
    }

    [Test]
    public void ToResponse_NumberValue_IsWrittenAsIs()
    {
        var response = Map(CreateEntry(Action.DetailsUpdated, Now, "{\"changes\":{\"priority\":{\"from\":1,\"to\":null}}}"), true);

        response.Changes.Should().BeEquivalentTo([new PatientTimelineChangeResponse { Field = "priority", From = "1", To = null }]);
    }

    [Test]
    public void ToResponse_Deleted_SaysWhatStatusItWasIn()
    {
        var response = TimelineMapper.ToResponse(new PatientTimelineRow(CreateEntry(Action.Deleted, Now, "{\"status\":\"Cancelled\"}"), null), true);

        response.Kind.Should().Be("Deleted");
        response.FromStatus.Should().Be("Cancelled");
    }

    [TestCase("not json")]
    [TestCase("[1,2]")]
    [TestCase("{}")]
    public void ToResponse_DetailsUnreadable_MapsTheEntryWithoutThem(string details)
    {
        foreach (var action in Enum.GetValues<Action>())
        {
            var response = Map(CreateEntry(action, Now, details), true);

            response.Action.Should().Be(action.ToString());
            response.Changes.Where(c => c.From is not null || c.To is not null).Should().BeEmpty();
            response.Reason.Should().BeNull();
        }
    }

    [Test]
    public void ToUpcomingResponse_Procedure_MapsItsDueDayInTheUk()
    {
        // 23:59:59 UK time (BST) on the 18th is 22:59:59 UTC.
        var procedure = CreateTimelineProcedure(dueDateUtc: DueDates.EndOfDayUtc(Today.AddDays(3)));

        TimelineMapper.ToUpcomingResponse(procedure, Now).Should().BeEquivalentTo(new PatientTimelineUpcomingResponse
        {
            ProcedureId = ProcedureId,
            Title = CurrentTitle,
            Status = "InProgress",
            Priority = "High",
            DueDate = Today.AddDays(3),
            IsOverdue = false,
        });
    }

    [Test]
    public void ToUpcomingResponse_PastDue_IsOverdue()
    {
        TimelineMapper.ToUpcomingResponse(CreateTimelineProcedure(dueDateUtc: Now.AddDays(-1)), Now).IsOverdue.Should().BeTrue();
    }

    [Test]
    public void ToUpcomingResponse_BlockedAndPastDue_IsNotOverdue()
    {
        var procedure = CreateTimelineProcedure(status: PatientProcedureStatus.Blocked, dueDateUtc: Now.AddDays(-1));

        TimelineMapper.ToUpcomingResponse(procedure, Now).IsOverdue.Should().BeFalse();
    }

    [Test]
    public void ToProcedureResponse_Procedure_IsNotDeleted()
    {
        // Created at 23:30 UTC in June: the next day in the UK.
        var procedure = CreateTimelineProcedure(createdAt: new DateTime(2026, 6, 14, 23, 30, 0, DateTimeKind.Utc));

        TimelineMapper.ToProcedureResponse(procedure).Should().BeEquivalentTo(
            new PatientTimelineProcedureResponse { Id = ProcedureId, Title = CurrentTitle, IsDeleted = false, CreatedOn = Today });
    }

    [Test]
    public void ToDeletedProcedureResponse_Deletion_IsTheProcedureAsItWasDeleted()
    {
        var deletion = CreateEntry(Action.Deleted, Now, procedureId: DeletedProcedureId, procedureTitle: DeletedTitle);

        TimelineMapper.ToDeletedProcedureResponse(deletion).Should().BeEquivalentTo(
            new PatientTimelineProcedureResponse { Id = DeletedProcedureId, Title = DeletedTitle, IsDeleted = true, DeletedOn = Today });
    }

    private static PatientTimelineEntryResponse Map(PatientProcedureAuditEntry entry, bool isAdmin) =>
        TimelineMapper.ToResponse(new PatientTimelineRow(entry, CurrentTitle), isAdmin);

    /// <summary>The one audit entry a change makes, as a handler would make it.</summary>
    private static PatientProcedureAuditEntry Audited(Action<ProcedureChange> act)
    {
        var change = new ProcedureChange(CreateProcedure(PatientProcedureStatus.InProgress), Staff, Now);
        act(change);
        return change.AuditEntries.Single();
    }

    /// <summary>Every detail edited at once, as UpdatePatientProcedureHandler audits it.</summary>
    private static PatientProcedureAuditEntry EditedDetails() => Audited(change => change.Audit(Action.DetailsUpdated, new
    {
        changes = new Dictionary<string, object>
        {
            ["title"] = new { from = ProcedureTitle, to = CurrentTitle },
            ["notes"] = new { from = "", to = NotesText },
            ["dueDate"] = new { from = Today, to = Today.AddDays(1) },
            ["priority"] = new { from = "Medium", to = "High" },
        },
    }));

    private static PatientTimelineEntryResponse MapAppointment(AppointmentEvent entry, bool isAdmin, bool procedureExists = true) =>
        TimelineMapper.ToResponse(new AppointmentTimelineRow(entry, procedureExists, procedureExists ? CurrentTitle : null), isAdmin);

    [TestCase(false)]
    [TestCase(true)]
    public void ToResponse_AppointmentEntry_MapsWhenWhoWhatTheProcedureNowAndTheAppointment(bool isAdmin)
    {
        var entry = CreateAppointmentEvent(AppointmentEventAction.Booked, Now);

        var response = MapAppointment(entry, isAdmin);

        response.Should().BeEquivalentTo(new PatientTimelineEntryResponse
        {
            Id = entry.Id,
            Kind = "Appointments",
            Action = "AppointmentBooked",
            AtUtc = Now,
            Username = Staff.Username,
            ProcedureId = ProcedureId,
            ProcedureTitle = CurrentTitle,
            IsProcedureDeleted = false,
            StageName = AppointmentStageName,
            AppointmentId = AppointmentId,
            AppointmentStartUtc = Now.AddDays(1),
            AppointmentEndUtc = Now.AddDays(1).AddMinutes(30),
            AppointmentStaffUsername = OtherUsername,
            IsAutomatic = false,
        });
    }

    [Test]
    public void ToResponse_AppointmentNotForAProcedure_HasNoProcedureAndIsNotDeleted()
    {
        var entry = CreateAppointmentEvent(AppointmentEventAction.Booked, Now, withProcedure: false);

        var response = TimelineMapper.ToResponse(new AppointmentTimelineRow(entry, false, null), isAdmin: false);

        response.ProcedureId.Should().BeNull();
        response.ProcedureTitle.Should().BeNull();
        response.IsProcedureDeleted.Should().BeFalse();
        response.StageName.Should().BeNull();
    }

    [Test]
    public void ToResponse_AppointmentOfADeletedProcedure_UsesTheTitleAtTheTimeAndSaysSo()
    {
        var entry = CreateAppointmentEvent(AppointmentEventAction.Booked, Now, procedureId: DeletedProcedureId, procedureTitle: DeletedTitle);

        var response = MapAppointment(entry, isAdmin: true, procedureExists: false);

        response.ProcedureId.Should().Be(DeletedProcedureId);
        response.ProcedureTitle.Should().Be(DeletedTitle);
        response.IsProcedureDeleted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_AutomaticRequest_SaysSo()
    {
        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Requested, Now, "{\"automatic\":true}"), false);

        response.Action.Should().Be("AppointmentRequested");
        response.IsAutomatic.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ToResponse_Rescheduled_GivesThePreviousTimeAndStaffToEveryone(bool isAdmin)
    {
        var details = "{\"previousStartUtc\":\"2026-06-14T09:30:00Z\",\"previousEndUtc\":\"2026-06-14T10:00:00Z\","
                      + "\"previousStaffUserId\":\"" + Staff.UserId + "\",\"previousStaffUsername\":\"" + Staff.Username + "\"}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Rescheduled, Now, details), isAdmin);

        response.Action.Should().Be("AppointmentRescheduled");
        response.PreviousStartUtc.Should().Be(new DateTime(2026, 6, 14, 9, 30, 0, DateTimeKind.Utc));
        response.PreviousStartUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        response.PreviousStaffUsername.Should().Be(Staff.Username);
    }

    [TestCase("\"2026-06-14T10:30:00+01:00\"")]
    [TestCase("\"2026-06-14T09:30:00\"")]
    [TestCase("\"2026-06-14T09:30:00.0000000Z\"")]
    public void ToResponse_RescheduledTimeWrittenAnyWay_IsReadAsUtc(string written)
    {
        var details = "{\"previousStartUtc\":" + written + "}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Rescheduled, Now, details), false);

        response.PreviousStartUtc.Should().Be(new DateTime(2026, 6, 14, 9, 30, 0, DateTimeKind.Utc));
        response.PreviousStartUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestCase("{}")]
    [TestCase("{\"previousStartUtc\":null,\"previousStaffUsername\":null}")]
    [TestCase("{\"previousStartUtc\":\"not a time\"}")]
    [TestCase("{\"previousStartUtc\":12}")]
    public void ToResponse_RescheduledFromNothingOrUnreadable_HasNoPreviousTime(string details)
    {
        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Rescheduled, Now, details), false);

        response.PreviousStartUtc.Should().BeNull();
        response.PreviousStaffUsername.Should().BeNull();
    }

    [TestCase(AppointmentEventAction.Attended, "AppointmentAttended", "Booked")]
    [TestCase(AppointmentEventAction.DidNotAttend, "AppointmentDidNotAttend", "Attended")]
    public void ToResponse_Outcome_GivesTheStatusBefore(AppointmentEventAction action, string expectedAction, string previous)
    {
        var response = MapAppointment(CreateAppointmentEvent(action, Now, "{\"previousStatus\":\"" + previous + "\"}"), false);

        response.Action.Should().Be(expectedAction);
        response.FromStatus.Should().Be(previous);
    }

    [Test]
    public void ToResponse_EnteredInError_GivesTheOutcomeItHad_AndTheReasonToAdminsOnly([Values] bool isAdmin)
    {
        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.EnteredInError, Now,
            "{\"reason\":\"Synthetic wrong patient.\",\"previousStatus\":\"Attended\"}"), isAdmin);

        response.Action.Should().Be("AppointmentEnteredInError");
        response.FromStatus.Should().Be("Attended");
        response.Reason.Should().Be(isAdmin ? "Synthetic wrong patient." : null);
    }

    [TestCase(false, "{\"outcome\":\"Attended\"}", null, "Attended")]
    [TestCase(false, "{\"outcome\":\"DidNotAttend\",\"previousStatus\":\"Requested\"}", "Requested", "DidNotAttend")]
    [TestCase(true, "{\"outcome\":\"Attended\",\"previousStatus\":\"Requested\"}", "Requested", "Attended")]
    public void ToResponse_RecordedAfterTheEvent_GivesTheOutcomeAndWhetherItWasRequested(
        bool isAdmin, string details, string? from, string to)
    {
        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.RecordedAfterEvent, Now, details), isAdmin);

        response.Kind.Should().Be("Appointments");
        response.Action.Should().Be("AppointmentRecordedAfterEvent");
        response.FromStatus.Should().Be(from);
        response.ToStatus.Should().Be(to);
    }

    [Test]
    public void ToResponse_CancelledForAnAdmin_IncludesTheReason()
    {
        var details = "{\"reason\":\"" + CancelReasonText + "\",\"automatic\":true}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Cancelled, Now, details), true);

        response.Action.Should().Be("AppointmentCancelled");
        response.Reason.Should().Be(CancelReasonText);
        response.IsAutomatic.Should().BeTrue();
    }

    [Test]
    public void ToResponse_CancelledForStaff_LeavesTheReasonOut()
    {
        var details = "{\"reason\":\"" + CancelReasonText + "\",\"automatic\":false}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.Cancelled, Now, details), false);

        response.Reason.Should().BeNull();
        response.IsAutomatic.Should().BeFalse();
    }

    [Test]
    public void ToResponse_AppointmentNotesForAnAdmin_IncludeTheOldAndNewText()
    {
        var details = "{\"from\":\"\",\"to\":\"" + NotesText + "\"}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.NotesUpdated, Now, details), true);

        response.Action.Should().Be("AppointmentNotesUpdated");
        response.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PatientTimelineChangeResponse { Field = "appointmentNotes", From = string.Empty, To = NotesText });
    }

    [Test]
    public void ToResponse_AppointmentNotesForStaff_SayOnlyThatTheyChanged()
    {
        var details = "{\"from\":\"" + NotesText + "\",\"to\":\"" + NotesText + " (edited)\"}";

        var response = MapAppointment(CreateAppointmentEvent(AppointmentEventAction.NotesUpdated, Now, details), false);

        response.Changes.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new PatientTimelineChangeResponse { Field = "appointmentNotes", From = null, To = null });
    }

    [TestCase("not json")]
    [TestCase("[1,2]")]
    [TestCase("{}")]
    public void ToResponse_AppointmentDetailsUnreadable_MapsTheEntryWithoutThem(string details)
    {
        foreach (var action in Enum.GetValues<AppointmentEventAction>())
        {
            var response = MapAppointment(CreateAppointmentEvent(action, Now, details), true);

            response.Action.Should().Be("Appointment" + action);
            response.IsAutomatic.Should().BeFalse();
            response.Reason.Should().BeNull();
            response.FromStatus.Should().BeNull();
            response.PreviousStartUtc.Should().BeNull();
            response.Changes.Where(c => c.From is not null || c.To is not null).Should().BeEmpty();
        }
    }

    [Test]
    public void ToResponse_StageFinishedWithoutVisitForAnAdmin_IncludesTheReason()
    {
        var response = Map(StageFinishedWithoutVisit(VisitRequirement.Required), true);

        response.FinishedWithoutVisit.Should().BeTrue();
        response.VisitRequirement.Should().Be("Required");
        response.Reason.Should().Be(WithoutVisitReason);
    }

    [Test]
    public void ToResponse_StageFinishedWithoutVisitForStaff_SaysSoWithoutTheReason()
    {
        var response = Map(StageFinishedWithoutVisit(VisitRequirement.Suggested), false);

        response.FinishedWithoutVisit.Should().BeTrue();
        response.VisitRequirement.Should().Be("Suggested");
        response.Reason.Should().BeNull();
    }

    [Test]
    public void ToResponse_StageFinishedWithItsVisit_SaysNothingAboutVisits()
    {
        var response = Map(CreateEntry(Action.StageFinished, Now, "{\"withoutVisit\":false,\"reason\":\"x\"}"), true);

        response.FinishedWithoutVisit.Should().BeFalse();
        response.VisitRequirement.Should().BeNull();
        response.Reason.Should().BeNull();
    }

    /// <summary>The StageFinished entry of a stage finished without its visit, with a reason, as the handler records it.</summary>
    private static PatientProcedureAuditEntry StageFinishedWithoutVisit(VisitRequirement requirement)
    {
        var change = new ProcedureChange(CreateProcedure(PatientProcedureStatus.InProgress), Admin, Now);
        change.FinishStage(change.Procedure.StageAt(1), new FinishedWithoutVisit(requirement, WithoutVisitReason));
        return change.AuditEntries.Single(e => e.Action == Action.StageFinished);
    }
}
