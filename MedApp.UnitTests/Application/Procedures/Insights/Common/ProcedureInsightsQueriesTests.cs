using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Insights.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Insights.Common;

[TestFixture]
public sealed class ProcedureInsightsQueriesTests
{
    private static readonly DateTime Moment = Now.AddDays(-1);

    // Status counts ------------------------------------------------------------------------------------------

    [Test]
    public void StatusCounts_Procedures_CountsEachStatusSplitByPastDue()
    {
        var procedures = new[]
        {
            CreateProcedure(Status.InProgress, dueDateUtc: Now.AddDays(-1)),
            CreateProcedure(Status.InProgress, dueDateUtc: Now.AddDays(1)),
            CreateProcedure(Status.InProgress, dueDateUtc: Now.AddDays(2)),
            CreateProcedure(Status.Blocked, dueDateUtc: Now.AddDays(-3)),
        };

        procedures.AsQueryable().StatusCounts(Now).Should().BeEquivalentTo([
            Count(Status.InProgress, 1, isPastDue: true),
            Count(Status.InProgress, 2),
            Count(Status.Blocked, 1, isPastDue: true),
        ]);
    }

    [TestCase(Status.Unassigned, Status.Assigned)]
    [TestCase(Status.Assigned, Status.Unassigned)]
    [TestCase(Status.InProgress, Status.Assigned)]
    [TestCase(Status.InReview, Status.Assigned)]
    [TestCase(Status.Completed, Status.Assigned)]
    [TestCase(Status.Cancelled, Status.Assigned)]
    [TestCase(Status.Blocked, Status.Assigned)]
    [TestCase(Status.Archived, Status.Assigned)]
    public void StatusCountsAt_LastChangeBeforeTheMoment_GivesTheStatusThen(Status then, Status from)
    {
        var procedure = CreateProcedure(from);
        var audit = ChangeStatus(procedure, then, Moment.AddHours(-1));

        CountsAt([procedure], audit).Should().Equal(Count(then, 1));
    }

    [Test]
    public void StatusCountsAt_NoChangesBeforeTheMoment_WasUnassigned()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var later = ChangeStatus(procedure, Status.Assigned, Moment.AddHours(1));

        CountsAt([procedure], later).Should().Equal(Count(Status.Unassigned, 1));
    }

    [Test]
    public void StatusCountsAt_SeveralChanges_UsesTheLastBeforeTheMoment()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var audit = ChangeStatus(procedure, Status.Blocked, Moment.AddDays(-2))
            .Concat(ChangeStatus(procedure, Status.Assigned, Moment.AddDays(-1)))
            .Concat(ChangeStatus(procedure, Status.Cancelled, Moment.AddHours(1)))
            .ToList();

        CountsAt([procedure], audit).Should().Equal(Count(Status.Assigned, 1));
    }

    [Test]
    public void StatusCountsAt_TwoChangesAtOnce_UsesTheOneAfterInProgress()
    {
        // Finishing the last stage of a procedure nobody had started: Assigned → InProgress → InReview, all at once.
        var procedure = CreateProcedure(Status.Assigned, finishedStages: 2);
        procedure.StageAt(3).StartedAtUtc = null;
        procedure.StageAt(3).IsCompleted = false;
        var change = new ProcedureChange(procedure, Staff, Moment.AddHours(-1));
        change.FinishStage(procedure.StageAt(3));

        CountsAt([procedure], change.AuditEntries.Reverse().ToList()).Should().Equal(Count(Status.InReview, 1));
    }

    [Test]
    public void StatusCountsAt_ProcedureCreatedAfterTheMoment_IsLeftOut()
    {
        var procedure = CreateProcedure(Status.Unassigned, createdAt: Moment.AddHours(1));

        CountsAt([procedure], []).Should().BeEmpty();
    }

    [Test]
    public void StatusCountsAt_DueBeforeTheMoment_IsPastDueThen()
    {
        var procedure = CreateProcedure(Status.Unassigned, dueDateUtc: Moment.AddHours(-1));

        CountsAt([procedure], []).Should().Equal(Count(Status.Unassigned, 1, isPastDue: true));
    }

    [Test]
    public void StatusCountsAt_OtherProceduresChanges_AreNotItsOwn()
    {
        var procedure = CreateProcedure(Status.Unassigned);
        var other = CreateProcedure(Status.Assigned);
        var audit = ChangeStatus(other, Status.Blocked, Moment.AddHours(-1));

        CountsAt([procedure], audit).Should().Equal(Count(Status.Unassigned, 1));
    }

    private static List<StatusCount> CountsAt(IEnumerable<PatientProcedure> procedures, IEnumerable<PatientProcedureAuditEntry> audit) =>
        procedures.AsQueryable().StatusCountsAt(audit.AsQueryable(), Moment).ToList();

    // Completions and creations ------------------------------------------------------------------------------

    [Test]
    public void Completions_Window_CountsChangesToCompletedFromStartToBeforeEnd()
    {
        var atStart = ChangeStatus(CreateProcedure(Status.InReview), Status.Completed, Moment);
        var atEnd = ChangeStatus(CreateProcedure(Status.InReview), Status.Completed, Now);
        var inside = ChangeStatus(CreateProcedure(Status.InReview), Status.Completed, Now.AddHours(-1));
        var cancelled = ChangeStatus(CreateProcedure(Status.Assigned), Status.Cancelled, Now.AddHours(-1));
        var audit = atStart.Concat(atEnd).Concat(inside).Concat(cancelled).AsQueryable();

        audit.Completions(Moment, Now).Should().BeEquivalentTo(atStart.Concat(inside));
    }

    [Test]
    public void Creations_Window_CountsCreatedEntriesFromStartToBeforeEnd()
    {
        var procedure = CreateProcedure();
        var inside = Created(procedure, Moment);
        var before = Created(procedure, Moment.AddTicks(-1));
        var atEnd = Created(procedure, Now);
        var finished = Finished(procedure, procedure.StageAt(1), Moment.AddHours(1));

        new[] { inside, before, atEnd, finished }.AsQueryable().Creations(Moment, Now).Should().Equal(inside);
    }

    [Test]
    public void CountByHour_Entries_CountsEachUtcHour()
    {
        var procedure = CreateProcedure();
        var day = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var entries = new[]
        {
            Created(procedure, day.AddHours(10).AddMinutes(15)),
            Created(procedure, day.AddHours(10).AddMinutes(45)),
            Created(procedure, day.AddHours(23)),
        };

        var hours = entries.AsQueryable().CountByHour().ToList();

        hours.Should().BeEquivalentTo([new HourCount(day, 10, 2), new HourCount(day, 23, 1)]);
        hours.Select(h => h.StartUtc).Should().BeEquivalentTo([day.AddHours(10), day.AddHours(23)]);
    }

    [Test]
    public void ForTemplate_AuditEntries_KeepsOnlyThoseOfTheTemplatesProcedures()
    {
        var mine = CreateProcedure();
        mine.TemplateId = TemplateId;
        var other = CreateProcedure();
        var deleted = CreateProcedure();
        deleted.TemplateId = TemplateId;
        var audit = new[] { Created(mine, Moment), Created(other, Moment), Created(deleted, Moment) }.AsQueryable();
        var procedures = new[] { mine, other }.AsQueryable();

        audit.ForTemplate(procedures, TemplateId).Select(e => e.PatientProcedureId).Should().Equal(mine.Id);
        audit.ForTemplate(procedures, null).Should().HaveCount(3);
    }

    [Test]
    public void ForTemplate_Procedures_KeepsOnlyThatTemplatesWhenGiven()
    {
        var mine = CreateProcedure();
        mine.TemplateId = TemplateId;
        var other = CreateProcedure();
        var procedures = new[] { mine, other }.AsQueryable();

        procedures.ForTemplate(TemplateId).Should().Equal(mine);
        procedures.ForTemplate(null).Should().Equal(mine, other);
    }

    // Overdue ------------------------------------------------------------------------------------------------

    [Test]
    public void Overdue_Procedures_KeepsOpenOnesPastTheirDueDate()
    {
        var overdue = CreateProcedure(Status.InReview, dueDateUtc: Now.AddDays(-1));
        var notDue = CreateProcedure(Status.InProgress, dueDateUtc: Now.AddDays(1));
        var blocked = CreateProcedure(Status.Blocked, dueDateUtc: Now.AddDays(-1));
        var completed = CreateProcedure(Status.Completed, dueDateUtc: Now.AddDays(-1));

        new[] { overdue, notDue, blocked, completed }.AsQueryable().Overdue(Now).Should().Equal(overdue);
    }

    [Test]
    public void CountByAssignee_Procedures_CountsEachAssigneesProcedures()
    {
        var both = CreateProcedure(Status.Assigned);
        both.Assignments.Add(new PatientProcedureAssignment { PatientProcedureId = both.Id, UserId = OtherUserId });
        var staffOnly = CreateProcedure(Status.InProgress);
        var nobody = CreateProcedure(Status.Unassigned);

        new[] { both, staffOnly, nobody }.AsQueryable().CountByAssignee().Should().BeEquivalentTo([
            new AssigneeCount(Staff.UserId, 2),
            new AssigneeCount(OtherUserId, 1),
        ]);
    }

    [Test]
    public void CountByPriority_Procedures_CountsEachPriority()
    {
        var procedures = new[]
        {
            CreateProcedure(priority: PatientProcedurePriority.Urgent),
            CreateProcedure(priority: PatientProcedurePriority.Urgent),
            CreateProcedure(priority: PatientProcedurePriority.Low),
        };

        procedures.AsQueryable().CountByPriority().Should().BeEquivalentTo([
            new PriorityCount(PatientProcedurePriority.Urgent, 2),
            new PriorityCount(PatientProcedurePriority.Low, 1),
        ]);
    }

    // Stage durations ----------------------------------------------------------------------------------------

    private static readonly DateTime CreatedUtc = Now.AddDays(-7);

    private static List<StageDurationRow> Durations(
        IEnumerable<PatientProcedure> procedures,
        IEnumerable<PatientProcedureAuditEntry> audit,
        DateTime? fromUtc = null)
    {
        var list = procedures.ToList();
        return list.AsQueryable()
            .StageDurations(list.SelectMany(p => p.Stages).AsQueryable(), audit.AsQueryable(), fromUtc ?? Now.AddDays(-30), Now)
            .ToList();
    }

    [Test]
    public void StageDurations_FirstStage_WaitsFromCreationAndWorksFromStartToFinish()
    {
        var procedure = CreateProcedure();
        var stage = procedure.StageAt(1).Times(CreatedUtc.AddHours(2), CreatedUtc.AddHours(5));

        var row = Durations([procedure], [Finished(procedure, stage, CreatedUtc.AddHours(5))]).Single();

        row.Should().Be(new StageDurationRow(
            procedure.TemplateId, TemplateName, 1, ConsultationDefinitionId, ConsultationStage, 1, 2 * 3600, 3 * 3600));
    }

    [Test]
    public void StageDurations_LaterStage_IsReachedWhenTheOneBeforeIsFirstFinished()
    {
        var procedure = CreateProcedure();
        var first = procedure.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(30));
        var second = procedure.StageAt(2).Times(CreatedUtc.AddHours(6), CreatedUtc.AddHours(8));
        var audit = new[]
        {
            // The first stage was finished, unlocked by an admin and finished again later: the first finish counts.
            Finished(procedure, first, CreatedUtc.AddHours(4)),
            Finished(procedure, first, CreatedUtc.AddHours(30)),
            Finished(procedure, second, CreatedUtc.AddHours(8)),
        };

        var rows = Durations([procedure], audit);

        rows.Single(r => r.StageOrder == 1).Should().Match<StageDurationRow>(r => r.AverageWorkingSeconds == 3 * 3600);
        rows.Single(r => r.StageOrder == 2).Should().Match<StageDurationRow>(r =>
            r.AverageWaitingSeconds == 2 * 3600 && r.AverageWorkingSeconds == 2 * 3600);
    }

    [Test]
    public void StageDurations_NoFinishInTheAuditLog_UsesTheStagesOwnTimes()
    {
        var procedure = CreateProcedure();
        procedure.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(3));
        procedure.StageAt(2).Times(CreatedUtc.AddHours(4), CreatedUtc.AddHours(10));

        var rows = Durations([procedure], []);

        rows.Single(r => r.StageOrder == 2).Should().Match<StageDurationRow>(r =>
            r.AverageWaitingSeconds == 3600 && r.AverageWorkingSeconds == 6 * 3600);
    }

    [Test]
    public void StageDurations_StartedBeforeTheOneBeforeWasFinished_DidNotWait()
    {
        var procedure = CreateProcedure();
        procedure.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddHours(5));
        procedure.StageAt(2).Times(CreatedUtc.AddHours(2), CreatedUtc.AddHours(6));

        var row = Durations([procedure], []).Single(r => r.StageOrder == 2);

        row.AverageWaitingSeconds.Should().Be(0);
        row.AverageWorkingSeconds.Should().Be(4 * 3600);
    }

    [Test]
    public void StageDurations_SeveralProcedures_AveragesEachTemplatesStages()
    {
        var a = CreateProcedure();
        var b = CreateProcedure();
        var other = CreateProcedure();
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
        var procedure = CreateProcedure();
        var before = procedure.StageAt(1).Times(CreatedUtc.AddHours(1), CreatedUtc.AddDays(2));
        procedure.StageAt(2).Times(CreatedUtc.AddDays(3), CreatedUtc.AddDays(4));
        var notStarted = procedure.StageAt(3);
        notStarted.IsCompleted = true;
        notStarted.CompletedAtUtc = CreatedUtc.AddDays(5);
        // Finished again in the window, but first finished before it.
        var audit = new[] { Finished(procedure, before, CreatedUtc.AddHours(2)) };

        var rows = Durations([procedure], audit, fromUtc: CreatedUtc.AddDays(1));

        rows.Select(r => r.StageOrder).Should().Equal(2);
    }

    [Test]
    public void StageDurations_FinishedAfterTheWindow_IsLeftOut()
    {
        var procedure = CreateProcedure();
        procedure.StageAt(1).Times(Now.AddHours(-1), Now.AddHours(1));

        Durations([procedure], []).Should().BeEmpty();
    }

    [Test]
    public void SecondsBetween_TwoMoments_ReturnsWholeSecondsEitherWay()
    {
        InsightsFunctions.SecondsBetween(Now, Now.AddMinutes(2).AddMilliseconds(500)).Should().Be(120);
        InsightsFunctions.SecondsBetween(Now.AddSeconds(90), Now).Should().Be(-90);
    }
}
