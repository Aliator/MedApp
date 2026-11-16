using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Contracts.Procedures.Timeline.Responses;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

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
        TimelineMapper.ToProcedureResponse(CreateTimelineProcedure()).Should().BeEquivalentTo(
            new PatientTimelineProcedureResponse { Id = ProcedureId, Title = CurrentTitle, IsDeleted = false });
    }

    [Test]
    public void ToDeletedProcedureResponse_Deletion_IsTheProcedureAsItWasDeleted()
    {
        var deletion = CreateEntry(Action.Deleted, Now, procedureId: DeletedProcedureId, procedureTitle: DeletedTitle);

        TimelineMapper.ToDeletedProcedureResponse(deletion).Should().BeEquivalentTo(
            new PatientTimelineProcedureResponse { Id = DeletedProcedureId, Title = DeletedTitle, IsDeleted = true });
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
}
