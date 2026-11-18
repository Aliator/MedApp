using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Insights.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Insights.Common;

[TestFixture]
public sealed class PathwayInsightsQueriesTests
{
    private static readonly DateTime Moment = Now.AddDays(-1);

    // Status counts ------------------------------------------------------------------------------------------

    [Test]
    public void StatusCounts_Pathways_CountsEachStatusSplitByPastDue()
    {
        var pathways = new[]
        {
            CreatePathway(Status.InProgress, dueDateUtc: Now.AddDays(-1)),
            CreatePathway(Status.InProgress, dueDateUtc: Now.AddDays(1)),
            CreatePathway(Status.InProgress, dueDateUtc: Now.AddDays(2)),
            CreatePathway(Status.Blocked, dueDateUtc: Now.AddDays(-3)),
        };

        pathways.AsQueryable().StatusCounts(Now).Should().BeEquivalentTo([
            Count(Status.InProgress, 1, isPastDue: true),
            Count(Status.InProgress, 2),
            Count(Status.Blocked, 1, isPastDue: true),
        ]);
    }

    [TestCase(Status.Unassigned, Status.Assigned)]
    [TestCase(Status.Assigned, Status.Unassigned)]
    [TestCase(Status.InProgress, Status.Blocked)]
    [TestCase(Status.InReview, Status.Completed)]
    [TestCase(Status.Completed, Status.Archived)]
    [TestCase(Status.Cancelled, Status.Archived)]
    [TestCase(Status.Blocked, Status.Cancelled)]
    [TestCase(Status.Archived, Status.Assigned)]
    public void StatusCountsAt_ChangeAfterTheMoment_WasWhatItChangedFrom(Status then, Status now)
    {
        var pathway = CreatePathway(then);
        var audit = ChangeStatus(pathway, now, Moment.AddHours(1));

        pathway.Status.Should().Be(now);
        CountsAt([pathway], audit).Should().Equal(Count(then, 1));
    }

    [Test]
    public void StatusCountsAt_NoChangeSinceTheMoment_IsItsStatusNow()
    {
        // Changes before the moment say nothing about it; nor does the lack of any (a pathway from before the log).
        var pathway = CreatePathway(Status.Assigned);
        var before = ChangeStatus(pathway, Status.Completed, Moment.AddDays(-1));
        var fromBeforeTheLog = CreatePathway(Status.Completed);

        CountsAt([pathway, fromBeforeTheLog], before).Should().Equal(Count(Status.Completed, 2));
    }

    [Test]
    public void StatusCountsAt_SeveralChangesSince_UsesTheFirst()
    {
        var pathway = CreatePathway(Status.InProgress);
        var audit = ChangeStatus(pathway, Status.Blocked, Moment.AddHours(1))
            .Concat(ChangeStatus(pathway, Status.InProgress, Moment.AddHours(2)))
            .Concat(ChangeStatus(pathway, Status.Cancelled, Moment.AddHours(3)))
            .Reverse()
            .ToList();

        CountsAt([pathway], audit).Should().Equal(Count(Status.InProgress, 1));
    }

    [TestCase(Status.Assigned)]
    [TestCase(Status.Unassigned)]
    public void StatusCountsAt_TwoChangesAtOnce_WasWhatTheFirstChangedFrom(Status then)
    {
        // Finishing the last stage of a pathway nobody had started: then → InProgress → InReview, all at once.
        var pathway = CreatePathway(then, finishedStages: 2);
        pathway.StageAt(3).StartedAtUtc = null;
        pathway.StageAt(3).IsCompleted = false;
        var change = new PathwayChange(pathway, Staff, Moment.AddHours(1));
        change.FinishStage(pathway.StageAt(3));

        pathway.Status.Should().Be(Status.InReview);
        CountsAt([pathway], change.AuditEntries).Should().Equal(Count(then, 1));
        CountsAt([pathway], change.AuditEntries.Reverse().ToList()).Should().Equal(Count(then, 1));
    }

    [Test]
    public void StatusCountsAt_PathwayCreatedAfterTheMoment_IsLeftOut()
    {
        var pathway = CreatePathway(Status.Unassigned, createdAt: Moment.AddHours(1));

        CountsAt([pathway], []).Should().BeEmpty();
    }

    [Test]
    public void StatusCountsAt_DueBeforeTheMoment_IsPastDueThen()
    {
        var pathway = CreatePathway(Status.Unassigned, dueDateUtc: Moment.AddHours(-1));

        CountsAt([pathway], []).Should().Equal(Count(Status.Unassigned, 1, isPastDue: true));
    }

    [Test]
    public void StatusCountsAt_DueDateMovedOnSince_WasPastDueThenByItsOldDate()
    {
        // Due on the moment's day before last, moved to next month since: it was overdue then.
        var pathway = CreatePathway(Status.Assigned, dueDateUtc: Now.AddDays(30));
        var audit = new[] { DueDateChanged(pathway, MomentDay.AddDays(-2), MomentDay.AddDays(30), Moment.AddHours(1)) };

        CountsAt([pathway], audit).Should().Equal(Count(Status.Assigned, 1, isPastDue: true));
    }

    [Test]
    public void StatusCountsAt_DueDateBroughtForwardSince_WasNotPastDueThen()
    {
        // Due today (the moment's day) when it was moved back a week: not past due at the moment, though it is now.
        var pathway = CreatePathway(Status.Assigned, dueDateUtc: Moment.AddDays(-7));
        var audit = new[] { DueDateChanged(pathway, MomentDay, MomentDay.AddDays(-7), Moment.AddHours(1)) };

        CountsAt([pathway], audit).Should().Equal(Count(Status.Assigned, 1));
    }

    [Test]
    public void StatusCountsAt_SeveralDetailChangesSince_UsesTheFirstThatMovedTheDueDate()
    {
        var pathway = CreatePathway(Status.Assigned, dueDateUtc: Now.AddDays(30));
        var audit = new[]
        {
            DetailsChanged(pathway, Moment.AddMinutes(30), new Dictionary<string, object> { ["title"] = new { from = "A", to = "B" } }),
            DueDateChanged(pathway, MomentDay.AddDays(5), MomentDay.AddDays(10), Moment.AddHours(2)),
            DueDateChanged(pathway, MomentDay.AddDays(-1), MomentDay.AddDays(5), Moment.AddHours(1)),
        };

        CountsAt([pathway], audit).Should().Equal(Count(Status.Assigned, 1, isPastDue: true));
    }

    [Test]
    public void StatusCountsAt_DueDateChangedBeforeTheMoment_UsesTheDueDateNow()
    {
        var pathway = CreatePathway(Status.Assigned, dueDateUtc: Now.AddDays(3));
        var audit = new[] { DueDateChanged(pathway, MomentDay.AddDays(-5), MomentDay.AddDays(4), Moment.AddHours(-1)) };

        CountsAt([pathway], audit).Should().Equal(Count(Status.Assigned, 1));
    }

    [Test]
    public void StatusCountsAt_OtherPathwaysChanges_AreNotItsOwn()
    {
        var pathway = CreatePathway(Status.Unassigned);
        var other = CreatePathway(Status.Assigned);
        var audit = ChangeStatus(other, Status.Blocked, Moment.AddHours(1))
            .Append(DueDateChanged(other, MomentDay.AddDays(-3), MomentDay, Moment.AddHours(1)));

        CountsAt([pathway], audit).Should().Equal(Count(Status.Unassigned, 1));
    }

    private static readonly DateOnly MomentDay = DueDates.ToUkDate(Moment);

    private static List<StatusCount> CountsAt(IEnumerable<PatientPathway> pathways, IEnumerable<PatientPathwayAuditEntry> audit) =>
        pathways.AsQueryable().StatusCountsAt(audit.AsQueryable(), Moment).ToList();

    // Completions and creations ------------------------------------------------------------------------------

    [Test]
    public void Completions_Window_CountsChangesToCompletedFromStartToBeforeEnd()
    {
        var atStart = ChangeStatus(CreatePathway(Status.InReview), Status.Completed, Moment);
        var atEnd = ChangeStatus(CreatePathway(Status.InReview), Status.Completed, Now);
        var inside = ChangeStatus(CreatePathway(Status.InReview), Status.Completed, Now.AddHours(-1));
        var cancelled = ChangeStatus(CreatePathway(Status.Assigned), Status.Cancelled, Now.AddHours(-1));
        var audit = atStart.Concat(atEnd).Concat(inside).Concat(cancelled).AsQueryable();

        audit.Completions(Moment, Now).Should().BeEquivalentTo(atStart.Concat(inside));
    }

    [Test]
    public void Creations_Window_CountsCreatedEntriesFromStartToBeforeEnd()
    {
        var pathway = CreatePathway();
        var inside = Created(pathway, Moment);
        var before = Created(pathway, Moment.AddTicks(-1));
        var atEnd = Created(pathway, Now);
        var finished = Finished(pathway, pathway.StageAt(1), Moment.AddHours(1));

        new[] { inside, before, atEnd, finished }.AsQueryable().Creations(Moment, Now).Should().Equal(inside);
    }

    [Test]
    public void CountByHour_Entries_CountsEachUtcHour()
    {
        var pathway = CreatePathway();
        var day = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            Created(pathway, day.AddHours(10).AddMinutes(15)),
            Created(pathway, day.AddHours(10).AddMinutes(45)),
            Created(pathway, day.AddHours(23)),
        };

        var hours = entries.AsQueryable().CountByHour().ToList();

        hours.Should().BeEquivalentTo([new HourCount(day, 10, 2), new HourCount(day, 23, 1)]);
        hours.Select(h => h.StartUtc).Should().BeEquivalentTo([day.AddHours(10), day.AddHours(23)]);
    }

    [Test]
    public void ForTemplate_AuditEntries_KeepsOnlyThoseOfTheTemplatesPathways()
    {
        var mine = CreatePathway();
        mine.TemplateId = TemplateId;
        var other = CreatePathway();
        var deleted = CreatePathway();
        deleted.TemplateId = TemplateId;
        var audit = new[] { Created(mine, Moment), Created(other, Moment), Created(deleted, Moment) }.AsQueryable();
        var pathways = new[] { mine, other }.AsQueryable();

        audit.ForTemplate(pathways, TemplateId).Select(e => e.PatientPathwayId).Should().Equal(mine.Id);
        audit.ForTemplate(pathways, null).Should().HaveCount(3);
    }

    [Test]
    public void ForTemplate_Pathways_KeepsOnlyThatTemplatesWhenGiven()
    {
        var mine = CreatePathway();
        mine.TemplateId = TemplateId;
        var other = CreatePathway();
        var pathways = new[] { mine, other }.AsQueryable();

        pathways.ForTemplate(TemplateId).Should().Equal(mine);
        pathways.ForTemplate(null).Should().Equal(mine, other);
    }

    // Overdue ------------------------------------------------------------------------------------------------

    [Test]
    public void Overdue_Pathways_KeepsOpenOnesPastTheirDueDate()
    {
        var overdue = CreatePathway(Status.InReview, dueDateUtc: Now.AddDays(-1));
        var notDue = CreatePathway(Status.InProgress, dueDateUtc: Now.AddDays(1));
        var blocked = CreatePathway(Status.Blocked, dueDateUtc: Now.AddDays(-1));
        var completed = CreatePathway(Status.Completed, dueDateUtc: Now.AddDays(-1));

        new[] { overdue, notDue, blocked, completed }.AsQueryable().Overdue(Now).Should().Equal(overdue);
    }

    [Test]
    public void CountByAssignee_Pathways_CountsEachAssigneesPathways()
    {
        var both = CreatePathway(Status.Assigned);
        both.Assignments.Add(new PatientPathwayAssignment { PatientPathwayId = both.Id, UserId = OtherUserId });
        var staffOnly = CreatePathway(Status.InProgress);
        var nobody = CreatePathway(Status.Unassigned);

        new[] { both, staffOnly, nobody }.AsQueryable().CountByAssignee().Should().BeEquivalentTo([
            new AssigneeCount(Staff.UserId, 2),
            new AssigneeCount(OtherUserId, 1),
        ]);
    }

    [Test]
    public void CountByPriority_Pathways_CountsEachPriority()
    {
        var pathways = new[]
        {
            CreatePathway(priority: PatientPathwayPriority.Urgent),
            CreatePathway(priority: PatientPathwayPriority.Urgent),
            CreatePathway(priority: PatientPathwayPriority.Low),
        };

        pathways.AsQueryable().CountByPriority().Should().BeEquivalentTo([
            new PriorityCount(PatientPathwayPriority.Urgent, 2),
            new PriorityCount(PatientPathwayPriority.Low, 1),
        ]);
    }

    // Stage durations ----------------------------------------------------------------------------------------

    private static readonly DateTime CreatedUtc = Now.AddDays(-7);

    private static List<StageDurationRow> Durations(
        IEnumerable<PatientPathway> pathways,
        IEnumerable<PatientPathwayAuditEntry> audit,
        DateTime? fromUtc = null)
    {
        var list = pathways.ToList();
        return list.AsQueryable()
            .StageDurations(list.SelectMany(p => p.Stages).AsQueryable(), audit.AsQueryable(), fromUtc ?? Now.AddDays(-30), Now)
            .ToList();
    }

    [Test]
    public void StageDurations_FirstStage_WaitsFromCreationAndWorksFromStartToFinish()
    {
        var pathway = CreatePathway();
        var stage = pathway.StageAt(1).Times(CreatedUtc.AddHours(2), CreatedUtc.AddHours(5));

        var row = Durations([pathway], [Finished(pathway, stage, CreatedUtc.AddHours(5))]).Single();

        row.Should().Be(new StageDurationRow(
            pathway.TemplateId, TemplateName, 1, ConsultationDefinitionId, ConsultationStage, 1, 2 * 3600, 3 * 3600));
    }

    [Test]
    public void StageDurations_LaterStage_IsReachedWhenTheOneBeforeIsFirstFinished()
    {
        var pathway = CreatePathway();
        var first = pathway.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(30));
        var second = pathway.StageAt(2).Times(CreatedUtc.AddHours(6), CreatedUtc.AddHours(8));
        var audit = new[]
        {
            // The first stage was finished, unlocked by an admin and finished again later: the first finish counts.
            Finished(pathway, first, CreatedUtc.AddHours(4)),
            Finished(pathway, first, CreatedUtc.AddHours(30)),
            Finished(pathway, second, CreatedUtc.AddHours(8)),
        };

        var rows = Durations([pathway], audit);

        rows.Single(r => r.StageOrder == 1).Should().Match<StageDurationRow>(r => r.AverageWorkingSeconds == 3 * 3600);
        rows.Single(r => r.StageOrder == 2).Should().Match<StageDurationRow>(r =>
            r.AverageWaitingSeconds == 2 * 3600 && r.AverageWorkingSeconds == 2 * 3600);
    }

    [Test]
    public void StageDurations_NoFinishInTheAuditLog_UsesTheStagesOwnTimes()
    {
        var pathway = CreatePathway();
        pathway.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(3));
        pathway.StageAt(2).Times(CreatedUtc.AddHours(4), CreatedUtc.AddHours(10));

        var rows = Durations([pathway], []);

        rows.Single(r => r.StageOrder == 2).Should().Match<StageDurationRow>(r =>
            r.AverageWaitingSeconds == 3600 && r.AverageWorkingSeconds == 6 * 3600);
    }

    [Test]
    public void StageDurations_StartedBeforeTheOneBeforeWasFinished_DidNotWait()
    {
        var pathway = CreatePathway();
        pathway.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(5));
        pathway.StageAt(2).Times(CreatedUtc.AddHours(2), CreatedUtc.AddHours(6));

        var row = Durations([pathway], []).Single(r => r.StageOrder == 2);

        row.AverageWaitingSeconds.Should().Be(0);
        row.AverageWorkingSeconds.Should().Be(4 * 3600);
    }

    [Test]
    public void StageDurations_SeveralPathways_AveragesEachTemplatesStages()
    {
        var a = CreatePathway();
        var b = CreatePathway();
        var other = CreatePathway();
        a.TemplateId = b.TemplateId = TemplateId;
        other.TemplateId = OtherTemplateId;
        other.TemplateName = OtherTemplateName;
        a.StageAt(1).Times(CreatedUtc.AddHours(2), CreatedUtc.AddHours(5));
        b.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(3));
        other.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(2));

        var rows = Durations([a, b, other], []);

        rows.Should().BeEquivalentTo([
            new StageDurationRow(TemplateId, TemplateName, 1, ConsultationDefinitionId, ConsultationStage, 2, 1.5 * 3600, 2.5 * 3600),
            new StageDurationRow(OtherTemplateId, OtherTemplateName, 1, ConsultationDefinitionId, ConsultationStage, 1, 3600, 3600),
        ]);
    }

    [Test]
    public void StageDurations_OnlyStagesFirstFinishedInTheWindowAndStarted_Count()
    {
        var pathway = CreatePathway();
        var before = pathway.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddDays(2));
        pathway.StageAt(2).Times(CreatedUtc.AddDays(3), CreatedUtc.AddDays(4));
        var notStarted = pathway.StageAt(3);
        notStarted.IsCompleted = true;
        notStarted.CompletedAtUtc = CreatedUtc.AddDays(5);
        // Finished again in the window, but first finished before it.
        var audit = new[] { Finished(pathway, before, CreatedUtc.AddHours(2)) };

        var rows = Durations([pathway], audit, fromUtc: CreatedUtc.AddDays(1));

        rows.Select(r => r.StageOrder).Should().Equal(2);
    }

    [Test]
    public void StageDurations_FinishedAfterTheWindow_IsLeftOut()
    {
        var pathway = CreatePathway();
        pathway.StageAt(1).Times(Now.AddHours(-1), Now.AddHours(1));

        Durations([pathway], []).Should().BeEmpty();
    }

    [Test]
    public void SecondsBetween_TwoMoments_ReturnsWholeSecondsEitherWay()
    {
        InsightsFunctions.SecondsBetween(Now, Now.AddMinutes(2).AddMilliseconds(500)).Should().Be(120);
        InsightsFunctions.SecondsBetween(Now.AddSeconds(90), Now).Should().Be(-90);
    }

    [TestCase("{\"changes\":{\"dueDate\":{\"from\":\"2026-06-12\"}}}", "$.changes.dueDate.from", "2026-06-12")]
    [TestCase("{\"a\":{\"n\":3,\"t\":true}}", "$.a.n", "3")]
    [TestCase("{\"a\":{\"n\":3,\"t\":true}}", "$.a.t", "true")]
    [TestCase("{\"a\":{\"b\":[1]}}", "$.a.b", null)]
    [TestCase("{\"a\":\"x\"}", "$.a.b", null)]
    [TestCase("{\"a\":1}", "$.b", null)]
    [TestCase("not json", "$.a", null)]
    [TestCase("", "$.a", null)]
    [TestCase(null, "$.a", null)]
    [TestCase("{\"a\":1}", "a", null)]
    public void JsonValue_PathInDocument_ReturnsItsTextOrNull(string? json, string path, string? expected)
    {
        InsightsFunctions.JsonValue(json, path).Should().Be(expected);
    }
}
