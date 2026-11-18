using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

[TestFixture]
public sealed class PathwaySearchTests
{
    private static readonly PatientPathway Overdue = CreatePathway(
        Status.InProgress, dueDateUtc: Now.AddDays(-2), priority: PatientPathwayPriority.High, title: "Knee replacement",
        patient: PatientsTestConstants.CreatePatient("Freya", "Ahmed", new DateOnly(1970, 1, 1), "f@example.com"));
    private static readonly PatientPathway BlockedPastDue = CreatePathway(
        Status.Blocked, dueDateUtc: Now.AddDays(-1), priority: PatientPathwayPriority.Low, title: "Colonoscopy");
    private static readonly PatientPathway Unassigned = CreatePathway(
        Status.Unassigned, dueDateUtc: Now.AddDays(5), priority: PatientPathwayPriority.High, title: "Hip replacement");
    private static readonly PatientPathway Completed = CreatePathway(
        Status.Completed, finishedStages: 3, dueDateUtc: Now.AddDays(-30), title: "Cataract surgery");
    private static readonly PatientPathway Archived = CreatePathway(
        Status.Archived, finishedStages: 3, dueDateUtc: Now.AddDays(-60), title: "Biopsy");

    private static IQueryable<PatientPathway> Pathways() =>
        new[] { Unassigned, Completed, Overdue, Archived, BlockedPastDue }.AsQueryable();

    [Test]
    public void Filter_NoFilters_HidesCompletedCancelledAndArchived()
    {
        Pathways().Filter(CreateSearchQuery(), Now).Should().BeEquivalentTo([Overdue, BlockedPastDue, Unassigned]);
    }

    [Test]
    public void Filter_IncludeClosed_ReturnsEveryPathway()
    {
        Pathways().Filter(CreateSearchQuery() with { IncludeClosed = true }, Now).Should().HaveCount(5);
    }

    [Test]
    public void Filter_Statuses_ReturnsOnlyThoseStatusesEvenIfClosed()
    {
        var query = CreateSearchQuery() with { Statuses = ["completed", "Blocked"] };

        Pathways().Filter(query, Now).Should().BeEquivalentTo([Completed, BlockedPastDue]);
    }

    [Test]
    public void Filter_Search_MatchesTitleOrPatientNameIgnoringCase()
    {
        Pathways().Filter(CreateSearchQuery() with { Search = " freya ahm " }, Now).Should().Equal(Overdue);
        Pathways().Filter(CreateSearchQuery() with { Search = "HIP" }, Now).Should().Equal(Unassigned);
    }

    [Test]
    public void Filter_TitleTemplateAndPatientName_EachMatchOnlyTheirOwnFieldIgnoringCase()
    {
        Pathways().Filter(CreateSearchQuery() with { Title = " REPLACEMENT " }, Now).Should().BeEquivalentTo([Overdue, Unassigned]);
        Pathways().Filter(CreateSearchQuery() with { Title = "freya" }, Now).Should().BeEmpty();
        Pathways().Filter(CreateSearchQuery() with { PatientName = "freya ahm" }, Now).Should().Equal(Overdue);
        Pathways().Filter(CreateSearchQuery() with { PatientName = "knee" }, Now).Should().BeEmpty();
        Pathways().Filter(CreateSearchQuery() with { TemplateName = "test temp" }, Now).Should().HaveCount(3);
        Pathways().Filter(CreateSearchQuery() with { TemplateName = "knee" }, Now).Should().BeEmpty();
    }

    [Test]
    public void Filter_OverdueOnly_LeavesOutBlockedAndClosedPathways()
    {
        var query = CreateSearchQuery() with { OverdueOnly = true, IncludeClosed = true };

        Pathways().Filter(query, Now).Should().Equal(Overdue);
    }

    [Test]
    public void Filter_AssignedToMe_ReturnsPathwaysTheUserIsAssignedTo()
    {
        var query = CreateSearchQuery() with { AssignedToMe = true };

        Pathways().Filter(query, Now).Should().BeEquivalentTo([Overdue, BlockedPastDue]);
    }

    [Test]
    public void Filter_Priority_ReturnsThatPriority()
    {
        Pathways().Filter(CreateSearchQuery() with { Priority = "high" }, Now).Should().BeEquivalentTo([Overdue, Unassigned]);
    }

    [Test]
    public void Filter_Priorities_ReturnsAnyOfThem()
    {
        var query = CreateSearchQuery() with { Priorities = ["low", "Urgent"] };

        Pathways().Filter(query, Now).Should().Equal(BlockedPastDue);
    }

    [Test]
    public void Filter_DueRange_IncludesWholeUkDaysAtBothEnds()
    {
        var due = DueDates.ToUkDate(BlockedPastDue.DueDateUtc);
        var query = CreateSearchQuery() with { DueFrom = due, DueTo = due };

        Pathways().Filter(query, Now).Should().Equal(BlockedPastDue);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Filter_Started_SplitsByWhetherAnyStageHasBegun(bool started)
    {
        var begun = CreatePathway(Status.InProgress);
        begun.StageAt(1).StartedAtUtc = Now.AddHours(-1);
        var finishedOne = CreatePathway(Status.Blocked, finishedStages: 1);
        var untouched = CreatePathway(Status.Assigned);
        var pathways = new[] { begun, finishedOne, untouched }.AsQueryable();

        var result = pathways.Filter(CreateSearchQuery() with { Started = started }, Now);

        if (started)
            result.Should().BeEquivalentTo([begun, finishedOne]);
        else
            result.Should().Equal(untouched);
    }

    [Test]
    public void Filter_TeamIds_ReturnsPathwaysInAnyOfThem()
    {
        var skin = Guid.NewGuid();
        var feet = Guid.NewGuid();
        var inSkin = CreatePathway(Status.Assigned);
        inSkin.TeamId = skin;
        var inFeet = CreatePathway(Status.InProgress);
        inFeet.TeamId = feet;
        var elsewhere = CreatePathway(Status.Assigned);
        elsewhere.TeamId = Guid.NewGuid();
        var pathways = new[] { inSkin, inFeet, elsewhere }.AsQueryable();

        pathways.Filter(CreateSearchQuery() with { TeamIds = [skin] }, Now).Should().Equal(inSkin);
        pathways.Filter(CreateSearchQuery() with { TeamIds = [skin, feet, skin] }, Now).Should().Equal(inSkin, inFeet);
        pathways.Filter(CreateSearchQuery() with { TeamIds = [] }, Now).Should().HaveCount(3);
    }

    [Test]
    public void Filter_StageDefinitionIds_ReturnsPathwaysWhoseCurrentStageComesFromThem()
    {
        // Stages: Consultation, Pathway, Consultation. Current: the first unfinished.
        var atFirstConsultation = CreatePathway(Status.Assigned);
        var atPathwayStage = CreatePathway(Status.InProgress, finishedStages: 1);
        var atLastConsultation = CreatePathway(Status.InProgress, finishedStages: 2);
        var allFinished = CreatePathway(Status.InReview, finishedStages: 3);
        var pathways = new[] { atFirstConsultation, atPathwayStage, atLastConsultation, allFinished }.AsQueryable();

        pathways.Filter(CreateSearchQuery() with { StageDefinitionIds = [PathwayDefinitionId] }, Now).Should().Equal(atPathwayStage);
        pathways.Filter(CreateSearchQuery() with { StageDefinitionIds = [ConsultationDefinitionId] }, Now)
            .Should().Equal(atFirstConsultation, atLastConsultation);
        pathways.Filter(CreateSearchQuery() with { StageDefinitionIds = [ConsultationDefinitionId, PathwayDefinitionId] }, Now)
            .Should().Equal(atFirstConsultation, atPathwayStage, atLastConsultation);
    }

    [Test]
    public void Filter_StageDefinitionIds_UsesStageOrderNotListOrder()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        pathway.Stages = pathway.Stages.OrderByDescending(s => s.StageOrder).ToList();

        new[] { pathway }.AsQueryable().Filter(CreateSearchQuery() with { StageDefinitionIds = [PathwayDefinitionId] }, Now)
            .Should().Equal(pathway);
    }

    [Test]
    public void Filter_StageDefinitionIds_CountsOnHoldButNeverClosedPathways()
    {
        var onHold = CreatePathway(Status.Blocked, finishedStages: 1);
        var cancelled = CreatePathway(Status.Cancelled, finishedStages: 1);
        var archived = CreatePathway(Status.Archived, finishedStages: 1);
        var pathways = new[] { onHold, cancelled, archived }.AsQueryable();

        // Even when closed ones are asked for, by status or with IncludeClosed: a closed pathway isn't waiting at a stage.
        var query = CreateSearchQuery() with { StageDefinitionIds = [PathwayDefinitionId] };
        pathways.Filter(query, Now).Should().Equal(onHold);
        pathways.Filter(query with { IncludeClosed = true }, Now).Should().Equal(onHold);
        pathways.Filter(query with { Statuses = ["Cancelled", "Blocked"] }, Now).Should().Equal(onHold);
    }

    [Test]
    public void Sort_Default_IsSoonestDueFirst()
    {
        Pathways().Sort(null, true).Should().Equal(Archived, Completed, Overdue, BlockedPastDue, Unassigned);
    }

    [Test]
    public void Sort_PriorityDescending_PutsHighestFirstAndBreaksTiesBySoonestDue()
    {
        Pathways().Filter(CreateSearchQuery(), Now).Sort(PathwaySearch.SortByPriority, false)
            .Should().Equal(Overdue, Unassigned, BlockedPastDue);
    }

    [Test]
    public void Sort_Patient_SortsByLastName()
    {
        Pathways().Filter(CreateSearchQuery(), Now).Sort("PATIENT", true).First().Should().Be(Overdue);
    }

    [Test]
    public void Filter_PatientTemplateAndAssignee_ReturnOnlyTheirPathways()
    {
        var assignee = Overdue.Assignments.Single().UserId;

        Pathways().Filter(CreateSearchQuery() with { PatientId = Overdue.PatientId }, Now).Should().Equal(Overdue);
        Pathways().Filter(CreateSearchQuery() with { TemplateId = Unassigned.TemplateId }, Now).Should().Equal(Unassigned);
        Pathways().Filter(CreateSearchQuery() with { AssignedToUserId = assignee }, Now).Should().Contain(Overdue).And.NotContain(Unassigned);
    }

    [Test]
    public void Filter_NobodyAssigned_ReturnsOnlyPathwaysWithNoAssignees()
    {
        Pathways().Filter(CreateSearchQuery() with { NobodyAssigned = true }, Now).Should().Equal(Unassigned);
    }

    [Test]
    public void Filter_UnknownStatusesAndPriorities_AreIgnored()
    {
        var everyOpen = Pathways().Filter(CreateSearchQuery(), Now).ToList();

        Pathways().Filter(CreateSearchQuery() with { Statuses = ["NotAStatus"] }, Now).Should().BeEquivalentTo(everyOpen);
        Pathways().Filter(CreateSearchQuery() with { Priorities = ["NotAPriority"] }, Now).Should().BeEquivalentTo(everyOpen);
    }

    [TestCase(PathwaySearch.SortByPriority, true)]
    [TestCase(PathwaySearch.SortByPriority, false)]
    [TestCase(PathwaySearch.SortByStatus, true)]
    [TestCase(PathwaySearch.SortByStatus, false)]
    [TestCase(PathwaySearch.SortByTitle, true)]
    [TestCase(PathwaySearch.SortByTitle, false)]
    [TestCase(PathwaySearch.SortByPatient, true)]
    [TestCase(PathwaySearch.SortByPatient, false)]
    [TestCase(PathwaySearch.SortByLastUpdated, true)]
    [TestCase(PathwaySearch.SortByLastUpdated, false)]
    [TestCase(PathwaySearch.SortByDueDate, true)]
    [TestCase(PathwaySearch.SortByDueDate, false)]
    [TestCase("not a column", false)]
    public void Sort_EachColumnEitherWay_OrdersByIt(string column, bool ascending)
    {
        Func<PatientPathway, IComparable> key = column switch
        {
            PathwaySearch.SortByPriority => p => p.Priority,
            PathwaySearch.SortByStatus => p => p.Status,
            PathwaySearch.SortByTitle => p => p.Title.ToLower(),
            PathwaySearch.SortByPatient => p => p.Patient.LastName.ToLower() + " " + p.Patient.FirstName.ToLower(),
            PathwaySearch.SortByLastUpdated => p => p.LastUpdated,
            _ => p => p.DueDateUtc,
        };

        var keys = Pathways().Sort(column, ascending).Select(key).ToList();

        if (ascending) keys.Should().BeInAscendingOrder();
        else keys.Should().BeInDescendingOrder();
    }

    /// <summary>A pathway accepted from a referral received <paramref name="receivedDaysAgo"/> days ago, with a 28-day target.</summary>
    private static PatientPathway WithClock(PatientPathway pathway, int receivedDaysAgo, int? pausedDaysAgo = null)
    {
        var referral = ReferralsTestConstants.CreateReferral(patient: pathway.Patient, status: MedApp.Domain.Referrals.ReferralStatus.Accepted);
        referral.ReceivedOn = Today.AddDays(-receivedDaysAgo);
        MedApp.Application.Referrals.Common.WaitingClock.Start(referral, 28);
        if (pausedDaysAgo is { } paused)
        {
            referral.PausedSinceOn = Today.AddDays(-paused);
            referral.PauseReviewOn = Today.AddDays(10);
        }
        referral.PathwayId = pathway.Id;
        pathway.Referral = referral;
        return pathway;
    }

    [Test]
    public void BreachedOnly_KeepsPathwaysWhoseClockIsPastItsTarget()
    {
        var breached = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 40);
        var within = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 5);
        var breachedThenPaused = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 60, pausedDaysAgo: 10);
        var pausedWithin = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 60, pausedDaysAgo: 50);
        var noClock = CreatePathway(Status.InProgress);

        new[] { breached, within, breachedThenPaused, pausedWithin, noClock }.AsQueryable()
            .Filter(CreateSearchQuery() with { BreachedOnly = true }, Now)
            .Should().BeEquivalentTo([breached, breachedThenPaused]);
    }

    [Test]
    public void Sort_DaysWaited_EitherWayWithPathwaysWithoutAClockLast()
    {
        var longest = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 40);
        var shortest = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 5);
        var paused = WithClock(CreatePathway(Status.InProgress), receivedDaysAgo: 30, pausedDaysAgo: 20);
        var noClock = CreatePathway(Status.InProgress);
        var pathways = new[] { noClock, shortest, longest, paused }.AsQueryable();

        pathways.Sort(PathwaySearch.SortByDaysWaited, false, Today).Should().Equal(longest, paused, shortest, noClock);
        pathways.Sort(PathwaySearch.SortByDaysWaited, true, Today).Should().Equal(shortest, paused, longest, noClock);
    }
}
