using FluentAssertions;
using MedApp.UnitTests.Common.Constants;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class ProcedureMapperTests
{
    [Test]
    public void ToResponse_ProcedureInProgress_MapsStagesInOrderWithTheirStateAndWhatCanBeDone()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1);
        procedure.StageAt(2).StartedAtUtc = Now.AddHours(-1);
        procedure.StageAt(2).StartedByUserId = OtherUserId;
        procedure.Stages.Reverse();

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now);

        response.Stages.Select(s => s.StageOrder).Should().Equal(1, 2, 3);
        response.Stages.Select(s => s.State).Should().Equal("Completed", "InProgress", "NotStarted");
        response.Stages.Select(s => s.IsCurrent).Should().Equal(false, true, false);
        response.Stages.Select(s => s.CanEdit).Should().Equal(false, true, false);
        response.Stages.Select(s => s.CanUnlock).Should().Equal(true, false, false);
        response.Stages[1].StartedByUsername.Should().Be(OtherUsername);
        response.Stages[0].CompletedByUsername.Should().Be(Staff.Username);
        response.CurrentStageId.Should().Be(procedure.StageAt(2).Id);
        response.CompletedStageCount.Should().Be(1);
        response.StageCount.Should().Be(3);
        response.AllowedStatusChanges.Should().Equal("Blocked", "Cancelled");
        response.CanWorkOnStages.Should().BeTrue();
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void ToResponse_Procedure_MapsTheDetailsPatientAndAssignees()
    {
        var procedure = CreateProcedure(Status.Assigned, dueDateUtc: DueDates.EndOfDayUtc(Today.AddDays(3)));

        var response = ProcedureMapper.ToResponse(procedure, Usernames, Now);

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
        var procedure = CreateProcedure(Status.Assigned);

        var response = ProcedureMapper.ToResponse(procedure, new Dictionary<Guid, string>(), Now);

        response.Assignments[0].Username.Should().Be(ProcedureMapper.DeletedUser);
    }

    [Test]
    public void ToSummary_Procedure_ShowsTheCurrentStageProgressAndAssignees()
    {
        var procedure = CreateProcedure(Status.InProgress, finishedStages: 1, dueDateUtc: Now.AddDays(-1));
        procedure.Assignments.Add(new PatientProcedureAssignment { UserId = Admin.UserId, AssignedByUserId = Admin.UserId });

        var summary = ProcedureMapper.ToSummary(procedure, Usernames, Now);

        summary.CurrentStageName.Should().Be(ProcedureStage);
        summary.CompletedStageCount.Should().Be(1);
        summary.StageCount.Should().Be(3);
        summary.IsOverdue.Should().BeTrue();
        summary.Assignees.Should().Equal(Admin.Username, Staff.Username);
    }
}
