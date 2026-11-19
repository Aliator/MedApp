using FluentAssertions;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TimelineTestConstants;
using Action = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Queries.GetPatientTimeline;

[TestFixture]
public sealed class PatientTimelineSearchTests
{
    private static readonly PatientPathway Pathway = CreateTimelinePathway();

    private static readonly PatientPathwayAuditEntry Created = CreateEntry(Action.Created, Now.AddDays(-3));
    private static readonly PatientPathwayAuditEntry Assigned = CreateEntry(Action.Assigned, Now.AddDays(-2));
    private static readonly PatientPathwayAuditEntry Notes = CreateEntry(Action.StageNotesUpdated, Now.AddDays(-1));
    private static readonly PatientPathwayAuditEntry OtherPatients = CreateEntry(Action.Created, Now, patientId: OtherPatientId);
    private static readonly PatientPathwayAuditEntry DeletedCreated =
        CreateEntry(Action.Created, Now.AddDays(-4), pathwayId: DeletedPathwayId, pathwayTitle: DeletedTitle);
    private static readonly PatientPathwayAuditEntry Deleted =
        CreateEntry(Action.Deleted, Now.AddHours(-1), pathwayId: DeletedPathwayId, pathwayTitle: DeletedTitle);

    private static List<PatientTimelineRow> Run(GetPatientTimelineQuery query, params PatientPathwayAuditEntry[] entries) =>
        PatientTimelineSearch.Page(entries.AsQueryable(), new[] { Pathway }.AsQueryable(), query).ToList();

    private static PatientPathwayAuditEntry[] All() => [Created, Assigned, Notes, OtherPatients, DeletedCreated, Deleted];

    [Test]
    public void Page_Staff_ReturnsThePatientsEntriesForPathwaysThatExistNewestFirst()
    {
        var rows = Run(CreateTimelineQuery(), All());

        rows.Select(r => r.Entry).Should().Equal(Notes, Assigned, Created);
    }

    [Test]
    public void Page_Admin_AlsoReturnsDeletedPathwaysEntries()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), All());

        rows.Select(r => r.Entry).Should().Equal(Deleted, Notes, Assigned, Created, DeletedCreated);
    }

    [Test]
    public void Page_Rows_CarryThePathwaysTitleNowOrNullOnceDeleted()
    {
        var rows = Run(CreateTimelineQuery(isAdmin: true), Created, Deleted);

        rows.Should().Equal(new PatientTimelineRow(Deleted, null), new PatientTimelineRow(Created, CurrentTitle));
    }

    [Test]
    public void Page_Consults_AreLeftOut_EvenWhenAskedFor()
    {
        var sent = CreateEntry(Action.ConsultRequested, Now.AddHours(-2));
        var replied = CreateEntry(Action.ConsultReplied, Now.AddHours(-1));

        Run(CreateTimelineQuery(isAdmin: true), [.. All(), sent, replied]).Select(r => r.Entry).Should().NotContain([sent, replied]);
        Run(CreateTimelineQuery() with { Kinds = ["Consults"] }, sent, replied).Should().BeEmpty();
    }

    [Test]
    public void Page_Kinds_ReturnsOnlyThoseKinds()
    {
        var query = CreateTimelineQuery() with { Kinds = ["notes", "Created"] };

        Run(query, All()).Select(r => r.Entry).Should().Equal(Notes, Created);
    }

    [Test]
    public void Page_PathwayIds_ReturnsOnlyThosePathways()
    {
        var query = CreateTimelineQuery(isAdmin: true) with { PathwayIds = [DeletedPathwayId, DeletedPathwayId] };

        Run(query, All()).Select(r => r.Entry).Should().Equal(Deleted, DeletedCreated);
    }

    [Test]
    public void Page_TakesOneMoreThanThePageSize()
    {
        Run(CreateTimelineQuery(pageSize: 1), All()).Should().HaveCount(2);
    }

    [Test]
    public void Page_EntriesAtTheSameMoment_AreOrderedByIdDescending()
    {
        var first = CreateEntry(Action.StageFinished, Now, id: Guid.Parse("00000022-0000-0000-0000-00000000000a"));
        var second = CreateEntry(Action.StatusChanged, Now, id: Guid.Parse("00000022-0000-0000-0000-00000000000b"));

        Run(CreateTimelineQuery(), first, second).Select(r => r.Entry).Should().Equal(second, first);
    }

    [Test]
    public void Page_FollowingEachCursor_ReturnsEveryEntryOnceEvenWhenTimesTie()
    {
        // Ten entries over four moments, so pages end in the middle of a moment.
        var entries = Enumerable.Range(0, 10)
            .Select(i => CreateEntry(Action.StageNotesUpdated, Now.AddMinutes(-(i % 4))))
            .ToArray();
        var seen = new List<PatientPathwayAuditEntry>();
        string? before = null;

        for (var pages = 0; pages < 10; pages++)
        {
            var rows = Run(CreateTimelineQuery(pageSize: 3) with { Before = before }, entries);
            var page = rows.Take(3).Select(r => r.Entry).ToList();
            seen.AddRange(page);
            if (rows.Count <= 3)
                break;
            before = TimelineCursor.After(page[^1]).Encode();
        }

        seen.Should().OnlyHaveUniqueItems().And.HaveCount(10);
        seen.Should().BeInDescendingOrder(e => e.AtUtc);
    }

    [Test]
    public void Page_UnreadableCursor_StartsFromTheNewest()
    {
        var query = CreateTimelineQuery() with { Before = "not a cursor" };

        Run(query, All()).Select(r => r.Entry).First().Should().Be(Notes);
    }
}
