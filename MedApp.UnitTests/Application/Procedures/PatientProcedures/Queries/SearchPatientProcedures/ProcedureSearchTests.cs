using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

[TestFixture]
public sealed class ProcedureSearchTests
{
    private static readonly PatientProcedure Overdue = CreateProcedure(
        Status.InProgress, dueDateUtc: Now.AddDays(-2), priority: PatientProcedurePriority.High, title: "Knee replacement",
        patient: PatientsTestConstants.CreatePatient("Freya", "Ahmed", new DateOnly(1970, 1, 1), "f@example.com"));
    private static readonly PatientProcedure BlockedPastDue = CreateProcedure(
        Status.Blocked, dueDateUtc: Now.AddDays(-1), priority: PatientProcedurePriority.Low, title: "Colonoscopy");
    private static readonly PatientProcedure Unassigned = CreateProcedure(
        Status.Unassigned, dueDateUtc: Now.AddDays(5), priority: PatientProcedurePriority.High, title: "Hip replacement");
    private static readonly PatientProcedure Completed = CreateProcedure(
        Status.Completed, finishedStages: 3, dueDateUtc: Now.AddDays(-30), title: "Cataract surgery");
    private static readonly PatientProcedure Archived = CreateProcedure(
        Status.Archived, finishedStages: 3, dueDateUtc: Now.AddDays(-60), title: "Biopsy");

    private static IQueryable<PatientProcedure> Procedures() =>
        new[] { Unassigned, Completed, Overdue, Archived, BlockedPastDue }.AsQueryable();

    [Test]
    public void Filter_NoFilters_HidesCompletedCancelledAndArchived()
    {
        Procedures().Filter(CreateSearchQuery(), Now).Should().BeEquivalentTo([Overdue, BlockedPastDue, Unassigned]);
    }

    [Test]
    public void Filter_IncludeClosed_ReturnsEveryProcedure()
    {
        Procedures().Filter(CreateSearchQuery() with { IncludeClosed = true }, Now).Should().HaveCount(5);
    }

    [Test]
    public void Filter_Statuses_ReturnsOnlyThoseStatusesEvenIfClosed()
    {
        var query = CreateSearchQuery() with { Statuses = ["completed", "Blocked"] };

        Procedures().Filter(query, Now).Should().BeEquivalentTo([Completed, BlockedPastDue]);
    }

    [Test]
    public void Filter_Search_MatchesTitleOrPatientNameIgnoringCase()
    {
        Procedures().Filter(CreateSearchQuery() with { Search = " freya ahm " }, Now).Should().Equal(Overdue);
        Procedures().Filter(CreateSearchQuery() with { Search = "HIP" }, Now).Should().Equal(Unassigned);
    }

    [Test]
    public void Filter_OverdueOnly_LeavesOutBlockedAndClosedProcedures()
    {
        var query = CreateSearchQuery() with { OverdueOnly = true, IncludeClosed = true };

        Procedures().Filter(query, Now).Should().Equal(Overdue);
    }

    [Test]
    public void Filter_AssignedToMe_ReturnsProceduresTheUserIsAssignedTo()
    {
        var query = CreateSearchQuery() with { AssignedToMe = true };

        Procedures().Filter(query, Now).Should().BeEquivalentTo([Overdue, BlockedPastDue]);
    }

    [Test]
    public void Filter_Priority_ReturnsThatPriority()
    {
        Procedures().Filter(CreateSearchQuery() with { Priority = "high" }, Now).Should().BeEquivalentTo([Overdue, Unassigned]);
    }

    [Test]
    public void Filter_DueRange_IncludesWholeUkDaysAtBothEnds()
    {
        var due = DueDates.ToUkDate(BlockedPastDue.DueDateUtc);
        var query = CreateSearchQuery() with { DueFrom = due, DueTo = due };

        Procedures().Filter(query, Now).Should().Equal(BlockedPastDue);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Filter_Started_SplitsByWhetherAnyStageHasBegun(bool started)
    {
        var begun = CreateProcedure(Status.InProgress);
        begun.StageAt(1).StartedAtUtc = Now.AddHours(-1);
        var finishedOne = CreateProcedure(Status.Blocked, finishedStages: 1);
        var untouched = CreateProcedure(Status.Assigned);
        var procedures = new[] { begun, finishedOne, untouched }.AsQueryable();

        var result = procedures.Filter(CreateSearchQuery() with { Started = started }, Now);

        if (started)
            result.Should().BeEquivalentTo([begun, finishedOne]);
        else
            result.Should().Equal(untouched);
    }

    [Test]
    public void Sort_Default_IsSoonestDueFirst()
    {
        Procedures().Sort(null, true).Should().Equal(Archived, Completed, Overdue, BlockedPastDue, Unassigned);
    }

    [Test]
    public void Sort_PriorityDescending_PutsHighestFirstAndBreaksTiesBySoonestDue()
    {
        Procedures().Filter(CreateSearchQuery(), Now).Sort(ProcedureSearch.SortByPriority, false)
            .Should().Equal(Overdue, Unassigned, BlockedPastDue);
    }

    [Test]
    public void Sort_Patient_SortsByLastName()
    {
        Procedures().Filter(CreateSearchQuery(), Now).Sort("PATIENT", true).First().Should().Be(Overdue);
    }
}
