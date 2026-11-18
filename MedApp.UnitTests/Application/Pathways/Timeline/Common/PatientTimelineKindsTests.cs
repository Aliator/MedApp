using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Common;

[TestFixture]
public sealed class PatientTimelineKindsTests
{
    [TestCase(Action.Created, PatientTimelineKind.Created)]
    [TestCase(Action.StageStarted, PatientTimelineKind.Stages)]
    [TestCase(Action.StageFinished, PatientTimelineKind.Stages)]
    [TestCase(Action.StageUnlocked, PatientTimelineKind.Stages)]
    [TestCase(Action.StageSkipped, PatientTimelineKind.Stages)]
    [TestCase(Action.BranchChosen, PatientTimelineKind.Stages)]
    [TestCase(Action.StageReinstated, PatientTimelineKind.Stages)]
    [TestCase(Action.StageNotesUpdated, PatientTimelineKind.Notes)]
    [TestCase(Action.Assigned, PatientTimelineKind.Assignments)]
    [TestCase(Action.Unassigned, PatientTimelineKind.Assignments)]
    [TestCase(Action.StatusChanged, PatientTimelineKind.Status)]
    [TestCase(Action.DetailsUpdated, PatientTimelineKind.Details)]
    [TestCase(Action.Deleted, PatientTimelineKind.Deleted)]
    [TestCase(Action.AttachmentAdded, PatientTimelineKind.Files)]
    [TestCase(Action.AttachmentDeleted, PatientTimelineKind.Files)]
    [TestCase(Action.AttachmentDownloaded, PatientTimelineKind.Files)]
    public void Of_Action_ReturnsItsKind(Action action, PatientTimelineKind expected)
    {
        PatientTimelineKinds.Of(CreateEntry(action, Now)).Should().Be(expected);
    }

    [Test]
    public void Of_UnknownAction_IsDetails()
    {
        PatientTimelineKinds.Of(CreateEntry((Action)99, Now)).Should().Be(PatientTimelineKind.Details);
    }

    [Test]
    public void Of_RealChanges_ReadsTheDetailsPathwayChangeWrites()
    {
        // Entries made the way handlers make them, so a change to how details are written breaks this test.
        var entries = RealEntries();

        entries.Select(e => (e.Action, PatientTimelineKinds.Of(e))).Should().Equal(
            (Action.StatusChanged, PatientTimelineKind.Blocks),
            (Action.StatusChanged, PatientTimelineKind.Blocks),
            (Action.StatusChanged, PatientTimelineKind.Status),
            (Action.DetailsUpdated, PatientTimelineKind.DueDates),
            (Action.DetailsUpdated, PatientTimelineKind.Details),
            (Action.StatusChanged, PatientTimelineKind.Blocks),
            (Action.StatusChanged, PatientTimelineKind.Status));
    }

    [Test]
    public void Of_TypedTextThatLooksLikeAMarker_DoesNotChangeTheKind()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var change = new PathwayChange(pathway, Staff, Now);
        change.ChangeStatus(PatientPathwayStatus.Cancelled, "\"to\":\"Blocked\"");
        change.Audit(Action.DetailsUpdated, new { changes = new Dictionary<string, object> { ["title"] = new { from = "a", to = "\"dueDate\":{" } } });

        change.AuditEntries.Select(PatientTimelineKinds.Of).Should().Equal(PatientTimelineKind.Status, PatientTimelineKind.Details);
    }

    [Test]
    public void WhereKindIn_EachKind_MatchesExactlyTheEntriesOfThatKind()
    {
        var entries = RealEntries()
            .Concat(Enum.GetValues<Action>().Select(a => CreateEntry(a, Now)))
            .ToList();

        foreach (var kind in Enum.GetValues<PatientTimelineKind>())
        {
            var expected = entries.Where(e => PatientTimelineKinds.Of(e) == kind);

            entries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { kind })
                .Should().BeEquivalentTo(expected, options => options.WithStrictOrdering(), kind.ToString());
        }
    }

    [Test]
    public void WhereKindIn_SeveralKinds_MatchesAnyOfThem()
    {
        var created = CreateEntry(Action.Created, Now);
        var notes = CreateEntry(Action.StageNotesUpdated, Now);
        var assigned = CreateEntry(Action.Assigned, Now);

        new[] { created, notes, assigned }.AsQueryable()
            .WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Created, PatientTimelineKind.Assignments })
            .Should().Equal(created, assigned);
    }

    [Test]
    public void Parse_NamesInAnyCase_ReturnsTheKindsIgnoringUnknownOnes()
    {
        PatientTimelineKinds.Parse(["notes", "DUEDATES", "Exploded", "Notes"])
            .Should().BeEquivalentTo([PatientTimelineKind.Notes, PatientTimelineKind.DueDates]);
    }

    [Test]
    public void Parse_Null_ReturnsNone()
    {
        PatientTimelineKinds.Parse(null).Should().BeEmpty();
    }

    [TestCase("Blocks", true)]
    [TestCase("stages", true)]
    [TestCase("3", false)]
    [TestCase("Exploded", false)]
    [TestCase(null, false)]
    public void IsKind_Value_SaysWhetherItIsAKindName(string? value, bool expected)
    {
        PatientTimelineKinds.IsKind(value).Should().Be(expected);
    }

    [Test]
    public void Names_ListsEveryKind()
    {
        PatientTimelineKinds.Names.Should().Be("Created, Stages, Notes, Assignments, Blocks, Status, DueDates, Details, Deleted, Appointments, Files, Referrals, WaitingTime, Specimens");
    }

    /// <summary>
    /// A block, an unblock, another status change, a block then a cancellation of the blocked pathway, a target date
    /// edit and a title edit, as handlers audit them.
    /// </summary>
    private static List<PatientPathwayAuditEntry> RealEntries()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var change = new PathwayChange(pathway, Staff, Now);
        change.ChangeStatus(PatientPathwayStatus.Blocked, Reason);
        change.ChangeStatus(PatientPathwayStatus.InProgress, null);
        change.ChangeStatus(PatientPathwayStatus.Cancelled, Reason);
        change.Audit(Action.DetailsUpdated, new
        {
            changes = new Dictionary<string, object>
            {
                ["title"] = new { from = "a", to = "b" },
                ["dueDate"] = new { from = Today, to = Today.AddDays(1) },
            },
        });
        change.Audit(Action.DetailsUpdated, new { changes = new Dictionary<string, object> { ["title"] = new { from = "a", to = "b" } } });
        // Blocked, then cancelled while blocked: the cancellation is a status change, not an unblock.
        var cancelled = new PathwayChange(CreatePathway(PatientPathwayStatus.InProgress), Staff, Now);
        cancelled.ChangeStatus(PatientPathwayStatus.Blocked, Reason);
        cancelled.ChangeStatus(PatientPathwayStatus.Cancelled, Reason);
        return change.AuditEntries.Concat(cancelled.AuditEntries).ToList();
    }

    [Test]
    public void WhereKindIn_OnlyAppointments_MatchesNoAuditEntry()
    {
        var entries = RealEntries()
            .Concat(Enum.GetValues<Action>().Select(a => CreateEntry(a, Now)))
            .ToList();

        entries.Select(PatientTimelineKinds.Of).Should().NotContain(PatientTimelineKind.Appointments);
        entries.AsQueryable().WhereKindIn(new HashSet<PatientTimelineKind> { PatientTimelineKind.Appointments }).Should().BeEmpty();
    }
}
