using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class StageVisitsAfterWithdrawalTests
{
    private static (PatientPathway Pathway, PatientPathwayStage Stage, Appointment Withdrawn) Given(
        PatientPathwayStatus status = PatientPathwayStatus.Assigned, VisitRequirement requirement = VisitRequirement.Required)
    {
        var pathway = PathwaysTestConstants.CreatePathway(status);
        var stage = pathway.StageAt(1);
        stage.VisitRequirement = requirement;
        var withdrawn = CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-1), 10), stageId: stage.Id);
        withdrawn.Status = AppointmentStatus.EnteredInError;
        return (pathway, stage, withdrawn);
    }

    [Test]
    public void TheCurrentStageWithNoOtherVisit_IsAskedForAgain()
    {
        var (pathway, stage, withdrawn) = Given();

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn])
            .Should().Be((true, $"The '{stage.Name}' stage's visit is back in Requires Appointment.", false));
    }

    [Test]
    public void AStageWithNoVisit_SaysNothing()
    {
        var (pathway, stage, withdrawn) = Given(requirement: VisitRequirement.None);

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn]).Should().Be((false, (string?)null, false));
    }

    [Test]
    public void AFinishedRequiredStage_LeftWithoutAVisit_SaysAnAdminCanUnlockIt_AndTellsOthers()
    {
        var (pathway, stage, withdrawn) = Given();
        stage.IsCompleted = true;

        var (again, message, finishedWithoutVisit) = StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn]);

        again.Should().BeFalse();
        finishedWithoutVisit.Should().BeTrue();
        message.Should().Be(
            $"The '{stage.Name}' stage is finished, and now has no attended visit. If it still needs its visit, someone who can unlock stages can unlock it, and then book one. " +
            "The pathway's assignees and those who can finish a stage without its visit are told.");
    }

    [Test]
    public void AFinishedSuggestedStage_LeftWithoutAVisit_SaysAnAdminCanUnlockIt_ButTellsNobodyElse()
    {
        var (pathway, stage, withdrawn) = Given(requirement: VisitRequirement.Suggested);
        stage.IsCompleted = true;

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn]).Should().Be((false,
            $"The '{stage.Name}' stage is finished, and now has no attended visit. If it still needs its visit, someone who can unlock stages can unlock it, and then book one.",
            false));
    }

    [Test]
    public void AFinishedStage_WithAnotherAttendedVisit_SaysSo()
    {
        // A walk-in recorded for a stage finished on another visit: withdrawing it leaves that one.
        var (pathway, stage, withdrawn) = Given();
        stage.IsCompleted = true;
        var other = CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-2), 10), stageId: stage.Id);
        var elsewhere = CreateAppointment(AppointmentStatus.Attended, At(Today.AddDays(-2), 11), stageId: Guid.NewGuid());

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn, other]).Should().Be(
            (false, $"The '{stage.Name}' stage is finished, and still has an attended visit.", false));
        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn, elsewhere]).FinishedWithoutVisit
            .Should().BeTrue("another stage's visit doesn't count");
    }

    [TestCase(PatientPathwayStatus.Blocked, "on hold")]
    [TestCase(PatientPathwayStatus.Cancelled, "cancelled")]
    public void APathwayNotUnderWay_SaysSo(PatientPathwayStatus status, string words)
    {
        var (pathway, stage, withdrawn) = Given(status);

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn])
            .Should().Be((false, $"No visit is asked for again while the pathway is {words}.", false));
    }

    [Test]
    public void ALaterStage_IsntAskedForYet()
    {
        var (pathway, _, withdrawn) = Given();
        var later = pathway.StageAt(2);
        later.VisitRequirement = VisitRequirement.Suggested;

        StageVisits.AfterVisitWithdrawn(pathway, later, [withdrawn])
            .Should().Be((false, $"The '{later.Name}' stage isn't the current stage, so no visit is asked for yet.", false));
    }

    [TestCase(AppointmentStatus.Requested, "requested")]
    [TestCase(AppointmentStatus.Booked, "booked")]
    [TestCase(AppointmentStatus.Attended, "attended")]
    public void AStageWithAnotherVisit_IsntAskedForAgain(AppointmentStatus status, string words)
    {
        var (pathway, stage, withdrawn) = Given();
        var other = CreateAppointment(AppointmentStatus.Booked, At(NextMonday, 10), stageId: stage.Id);
        other.Status = status;

        StageVisits.AfterVisitWithdrawn(pathway, stage, [withdrawn, other])
            .Should().Be((false, $"The '{stage.Name}' stage already has a visit that's {words}, so no other is asked for.", false));
    }
}
