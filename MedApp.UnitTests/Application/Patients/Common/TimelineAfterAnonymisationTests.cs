using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>
/// The timeline is built from the procedure audit, so anonymising a patient must leave nothing of the free text it
/// removed in what the timeline shows, to admins (who see free text) or staff, for procedures that still exist or have
/// been deleted. Every action is recorded as handlers record it, with one unique string as all its free text (as
/// <c>TimelineStaffCanaryTests</c> does), then the record is anonymised and the timeline read back.
/// </summary>
[TestFixture]
public sealed class TimelineAfterAnonymisationTests
{
    private const string Canary = "canary-text-anonymised-41c9";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public void Apply_TimelineForAdminsAndStaff_HoldsNoneOfTheRemovedFreeText()
    {
        var (record, procedure) = AnonymisedRecord();

        foreach (var entry in record.ProcedureHistory)
        foreach (var isAdmin in new[] { true, false })
        {
            // The procedure as it is now, and as if it had since been deleted (its title then comes from the entry).
            Serialised(new PatientTimelineRow(entry, procedure.Title), isAdmin).Should().NotContain(Canary, $"{entry.Action}, admin: {isAdmin}");
            Serialised(new PatientTimelineRow(entry, null), isAdmin).Should().NotContain(Canary, $"{entry.Action} (deleted), admin: {isAdmin}");
        }
    }

    [Test]
    public void Apply_TimelineOverview_HoldsNoneOfTheRemovedFreeText()
    {
        var (record, procedure) = AnonymisedRecord();

        JsonSerializer.Serialize(TimelineMapper.ToProcedureResponse(procedure), Json).Should().NotContain(Canary);
        JsonSerializer.Serialize(TimelineMapper.ToUpcomingResponse(procedure, Now), Json).Should().NotContain(Canary);
        var deletion = record.ProcedureHistory.Single(e => e.Action == Action.Deleted);
        JsonSerializer.Serialize(TimelineMapper.ToDeletedProcedureResponse(deletion), Json).Should().NotContain(Canary);
    }

    [Test]
    public void Apply_KeepsEachEntrysTimelineKind()
    {
        var before = Entries(CreateProcedure(Status.Assigned, title: Canary)).Select(PatientTimelineKinds.Of).ToList();

        var (record, _) = AnonymisedRecord();

        record.ProcedureHistory.Select(PatientTimelineKinds.Of).Should().Equal(before);
    }

    [Test]
    public void Apply_KeepsTheStructuredValuesTheTimelineShows()
    {
        var (record, _) = AnonymisedRecord();

        var edit = TimelineMapper.ToResponse(new PatientTimelineRow(record.ProcedureHistory.Single(e => e.Action == Action.DetailsUpdated), null), isAdmin: true);
        edit.Changes.Should().Contain(c => c.Field == "priority" && c.From == "Medium" && c.To == "High");
        edit.Changes.Should().Contain(c => c.Field == "dueDate" && c.From == Today.ToString("yyyy-MM-dd"));
        var created = TimelineMapper.ToResponse(new PatientTimelineRow(record.ProcedureHistory.Single(e => e.Action == Action.Created), null), isAdmin: true);
        created.TemplateName.Should().Be(TemplateName);
    }

    private static string Serialised(PatientTimelineRow row, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(row, isAdmin), Json);

    /// <summary>A patient with one procedure whose every piece of free text is the canary, with its whole audit trail, anonymised.</summary>
    private static (PatientRecord Record, PatientProcedure Procedure) AnonymisedRecord()
    {
        var procedure = CreateProcedure(Status.Assigned, title: Canary);
        var history = Entries(procedure);
        var patient = new Patient
        {
            Id = procedure.PatientId,
            FirstName = "Synthetic",
            LastName = "Person",
            DateOfBirth = new DateOnly(1980, 5, 6),
            Email = "synthetic.person@example.com",
        };
        var record = new PatientRecord
        {
            Patient = patient,
            Procedures = [procedure],
            ProcedureHistory = history,
            Activity = [],
            Notifications = [],
        };

        PatientAnonymiser.Apply(record, Now);
        return (record, procedure);
    }

    /// <summary>Every action, recorded through <see cref="ProcedureChange"/> with the canary as all its free text.</summary>
    private static List<PatientProcedureAuditEntry> Entries(PatientProcedure procedure)
    {
        procedure.Notes = Canary;
        procedure.StageAt(1).Notes = Canary;
        var change = new ProcedureChange(procedure, Staff, Now);

        change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 });
        change.UpdateStageNotes(procedure.StageAt(1), $"{Canary} (edited)");
        change.FinishStage(procedure.StageAt(1));
        change.UnlockStage(procedure.StageAt(1));
        change.ChangeStatus(Status.Blocked, Canary);
        change.ChangeStatus(Status.InProgress, Canary);
        change.ChangeStatus(Status.Cancelled, Canary);
        change.Unassign(procedure.Assignments[0], Staff.Username);
        change.Assign(OtherUserId, OtherUsername);
        change.Audit(Action.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["title"] = new { from = Canary, to = $"{Canary} (edited)" },
                ["notes"] = new { from = Canary, to = $"{Canary} (edited)" },
                ["dueDate"] = new { from = Today, to = Today.AddDays(1) },
                ["priority"] = new { from = "Medium", to = "High" },
            },
        });
        change.Audit(Action.Deleted, new { status = procedure.Status.ToString() });
        return change.AuditEntries.ToList();
    }
}
