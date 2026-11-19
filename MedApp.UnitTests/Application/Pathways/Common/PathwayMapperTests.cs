using FluentAssertions;
using MedApp.UnitTests.Common.Constants;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Pathways.Common;

[TestFixture]
public sealed class PathwayMapperTests
{
    [Test]
    public void ToResponse_PathwayInProgress_MapsStagesInOrderWithTheirStateAndWhatCanBeDone()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        pathway.StageAt(2).StartedAtUtc = Now.AddHours(-1);
        pathway.StageAt(2).StartedByUserId = OtherUserId;
        pathway.Stages.Reverse();

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now);

        response.Stages.Select(s => s.StageOrder).Should().Equal(1, 2, 3);
        response.Stages.Select(s => s.State).Should().Equal("Completed", "InProgress", "NotStarted");
        response.Stages.Select(s => s.IsCurrent).Should().Equal(false, true, false);
        response.Stages.Select(s => s.CanEdit).Should().Equal(false, true, false);
        response.Stages.Select(s => s.CanUnlock).Should().Equal(true, false, false);
        response.Stages[1].StartedByUsername.Should().Be(OtherUsername);
        response.Stages[0].CompletedByUsername.Should().Be(Staff.Username);
        response.CurrentStageId.Should().Be(pathway.StageAt(2).Id);
        response.CompletedStageCount.Should().Be(1);
        response.StageCount.Should().Be(3);
        response.AllowedStatusChanges.Should().Equal("Blocked", "Cancelled");
        response.CanWorkOnStages.Should().BeTrue();
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void ToResponse_ViewerWithoutWorkPathways_CantWorkOnItOrChangeItsStatus()
    {
        // A Manager assigns work but doesn't do it, nor unlock stages.
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: StandardRoles.Manager.Permissions);

        response.CanWorkOnStages.Should().BeFalse();
        response.CanEditDetails.Should().BeFalse();
        response.CanAddNotes.Should().BeFalse();
        response.AllowedStatusChanges.Should().BeEmpty();
        response.Stages.Should().OnlyContain(s => !s.CanEdit && !s.CanUnlock);
        response.CanChangeAssignments.Should().BeTrue();
    }

    [Test]
    public void ToResponse_ViewerWhoWorksOnPathways_CanWorkOnItButNotAssignOrUnlock()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: StandardRoles.Nurse.Permissions);

        response.CanWorkOnStages.Should().BeTrue();
        response.CanEditDetails.Should().BeTrue();
        response.CanAddNotes.Should().BeTrue();
        response.AllowedStatusChanges.Should().Equal("Blocked", "Cancelled");
        response.Stages.Select(s => s.CanEdit).Should().Equal(false, true, false);
        response.CanChangeAssignments.Should().BeFalse();
        response.Stages.Should().OnlyContain(s => !s.CanUnlock);
    }

    [Test]
    public void ToResponse_ViewerWhoOverridesStages_CanUnlockAFinishedStage()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: StandardRoles.Clinician.Permissions);

        response.Stages.Select(s => s.CanUnlock).Should().Equal(true, false, false);
    }

    [Test]
    public void ToResponse_CancelledPathway_CanBeDeletedOnlyByAHolderOfDeletePathways()
    {
        var pathway = CreatePathway(Status.Cancelled);

        PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: new HashSet<Permission> { Permission.DeletePathways })
            .CanBeDeleted.Should().BeTrue();
        PathwayMapper.ToResponse(pathway, Usernames, Now, viewerPermissions: StandardRoles.Clinician.Permissions)
            .CanBeDeleted.Should().BeFalse();
        // No viewer known: only what the pathway allows.
        PathwayMapper.ToResponse(pathway, Usernames, Now).CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Pathway_MapsTheDetailsPatientAndAssignees()
    {
        var pathway = CreatePathway(Status.Assigned, dueDateUtc: DueDates.EndOfDayUtc(Today.AddDays(3)));

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now);

        response.PatientName.Should().Be($"{PatientsTestConstants.ValidFirstName} {PatientsTestConstants.ValidLastName}");
        response.TemplateName.Should().Be(TemplateName);
        response.DueDate.Should().Be(Today.AddDays(3));
        response.Priority.Should().Be("Medium");
        response.Status.Should().Be("Assigned");
        response.IsOverdue.Should().BeFalse();
        response.Assignments.Should().ContainSingle();
        response.Assignments[0].Username.Should().Be(Staff.Username);
        response.Assignments[0].AssignedByUsername.Should().Be(Admin.Username);
    }

    [Test]
    public void ToResponse_AssigneeWithoutAnAccount_ShowsDeletedUser()
    {
        var pathway = CreatePathway(Status.Assigned);

        var response = PathwayMapper.ToResponse(pathway, new Dictionary<Guid, string>(), Now);

        response.Assignments[0].Username.Should().Be(PathwayMapper.DeletedUser);
    }

    [Test]
    public void ToSummary_Pathway_ShowsTheCurrentStageProgressAndAssignees()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1, dueDateUtc: Now.AddDays(-1));
        pathway.Assignments.Add(new PatientPathwayAssignment { UserId = Admin.UserId, AssignedByUserId = Admin.UserId });

        var summary = PathwayMapper.ToSummary(pathway, Usernames, Now);

        summary.CurrentStageName.Should().Be(PathwayStage);
        summary.CompletedStageCount.Should().Be(1);
        summary.StageCount.Should().Be(3);
        summary.IsOverdue.Should().BeTrue();
        summary.Assignees.Should().Equal(Admin.Username, Staff.Username);
    }

    [Test]
    public void ToSummary_EveryStageFinished_HasNoCurrentStage()
    {
        var pathway = CreatePathway(Status.InReview, finishedStages: 3);

        var summary = PathwayMapper.ToSummary(pathway, Usernames, Now);

        summary.CurrentStageName.Should().BeNull();
        summary.CompletedStageCount.Should().Be(3);
    }

    [Test]
    public void ToSummary_PatientNotLoaded_HasAnEmptyPatientName()
    {
        var pathway = CreatePathway(Status.Assigned);
        pathway.Patient = null!;

        var summary = PathwayMapper.ToSummary(pathway, Usernames, Now);

        summary.PatientName.Should().BeEmpty();
        summary.PatientId.Should().Be(pathway.PatientId);
    }

    [Test]
    public void PatientName_NoPatient_IsEmpty()
    {
        PathwayMapper.PatientName(null).Should().BeEmpty();
    }

    [Test]
    public void ToResponse_StagesWithVisits_GiveEachItsVisitRequirementByName()
    {
        var pathway = CreatePathway(Status.InProgress);
        pathway.StageAt(1).VisitRequirement = VisitRequirement.Required;
        pathway.StageAt(3).VisitRequirement = VisitRequirement.Suggested;

        var response = PathwayMapper.ToResponse(pathway, Usernames, Now);

        response.Stages.Select(s => s.VisitRequirement).Should().Equal("Required", "None", "Suggested");
    }
}
