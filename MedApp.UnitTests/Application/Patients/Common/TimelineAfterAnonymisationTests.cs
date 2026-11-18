using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>
/// The timeline is built from the pathway audit, so anonymising a patient must leave nothing of the free text it
/// removed in what the timeline shows, to admins (who see free text) or staff, for pathways that still exist or have
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
        var (record, pathway) = AnonymisedRecord();

        foreach (var entry in record.PathwayHistory)
        foreach (var isAdmin in new[] { true, false })
        {
            // The pathway as it is now, and as if it had since been deleted (its title then comes from the entry).
            Serialised(new PatientTimelineRow(entry, pathway.Title), isAdmin).Should().NotContain(Canary, $"{entry.Action}, admin: {isAdmin}");
            Serialised(new PatientTimelineRow(entry, null), isAdmin).Should().NotContain(Canary, $"{entry.Action} (deleted), admin: {isAdmin}");
        }
    }

    [Test]
    public void Apply_TimelineOverview_HoldsNoneOfTheRemovedFreeText()
    {
        var (record, pathway) = AnonymisedRecord();

        JsonSerializer.Serialize(TimelineMapper.ToPathwayResponse(pathway), Json).Should().NotContain(Canary);
        JsonSerializer.Serialize(TimelineMapper.ToUpcomingResponse(pathway, Now), Json).Should().NotContain(Canary);
        var deletion = record.PathwayHistory.Single(e => e.Action == Action.Deleted);
        JsonSerializer.Serialize(TimelineMapper.ToDeletedPathwayResponse(deletion), Json).Should().NotContain(Canary);
    }

    [Test]
    public void Apply_KeepsEachEntrysTimelineKind()
    {
        var before = Entries(CreatePathway(Status.Assigned, title: Canary)).Select(PatientTimelineKinds.Of).ToList();

        var (record, _) = AnonymisedRecord();

        record.PathwayHistory.Select(PatientTimelineKinds.Of).Should().Equal(before);
    }

    [Test]
    public void Apply_KeepsTheStructuredValuesTheTimelineShows()
    {
        var (record, _) = AnonymisedRecord();

        var edit = TimelineMapper.ToResponse(new PatientTimelineRow(record.PathwayHistory.Single(e => e.Action == Action.DetailsUpdated), null), isAdmin: true);
        edit.Changes.Should().Contain(c => c.Field == "priority" && c.From == "Medium" && c.To == "High");
        edit.Changes.Should().Contain(c => c.Field == "dueDate" && c.From == Today.ToString("yyyy-MM-dd"));
        var created = TimelineMapper.ToResponse(new PatientTimelineRow(record.PathwayHistory.Single(e => e.Action == Action.Created), null), isAdmin: true);
        created.TemplateName.Should().Be(TemplateName);
    }

    private static string Serialised(PatientTimelineRow row, bool isAdmin) =>
        JsonSerializer.Serialize(TimelineMapper.ToResponse(row, isAdmin), Json);

    /// <summary>A patient with one pathway whose every piece of free text is the canary, with its whole audit trail, anonymised.</summary>
    private static (PatientRecord Record, PatientPathway Pathway) AnonymisedRecord()
    {
        var pathway = CreatePathway(Status.Assigned, title: Canary);
        var history = Entries(pathway);
        var patient = new Patient
        {
            Id = pathway.PatientId,
            FirstName = "Synthetic",
            LastName = "Person",
            DateOfBirth = new DateOnly(1980, 5, 6),
            Email = "synthetic.person@example.com",
        };
        var record = new PatientRecord
        {
            Patient = patient,
            Pathways = [pathway],
            PathwayHistory = history,
            Activity = [],
            Appointments = [],
            AppointmentHistory = [],
            Notifications = [],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = []
        };

        PatientAnonymiser.Apply(record, Now);
        return (record, pathway);
    }

    /// <summary>Every action, recorded through <see cref="PathwayChange"/> with the canary as all its free text.</summary>
    private static List<PatientPathwayAuditEntry> Entries(PatientPathway pathway)
    {
        pathway.LegacyNotes = Canary;
        pathway.StageAt(1).Notes = Canary;
        var change = new PathwayChange(pathway, Staff, Now);

        change.Audit(Action.Created, new { template = TemplateName, dueDate = Today, priority = "High", stageCount = 3 });
        change.UpdateStageNotes(pathway.StageAt(1), $"{Canary} (edited)");
        change.FinishStage(pathway.StageAt(1));
        change.UnlockStage(pathway.StageAt(1));
        change.ChangeStatus(Status.Blocked, Canary);
        change.ChangeStatus(Status.InProgress, Canary);
        change.ChangeStatus(Status.Cancelled, Canary);
        change.Unassign(pathway.Assignments[0], Staff.Username);
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
        change.Audit(Action.Deleted, new { status = pathway.Status.ToString() });
        return change.AuditEntries.ToList();
    }
}
