using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

[TestFixture]
public sealed class UnassignPatientProcedureHandlerTests : ProcedureHandlerTestBase
{
    private UnassignPatientProcedureHandler Handler => Fixture.Create<UnassignPatientProcedureHandler>();

    [Test]
    public async Task Handle_UserNotAssigned_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var act = () => Handler.Handle(new UnassignPatientProcedureCommand(procedure.Id, OtherUserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_LastAssignee_UnassignsThemAndMovesToUnassigned()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var response = await Handler.Handle(new UnassignPatientProcedureCommand(procedure.Id, Staff.UserId, Admin), CancellationToken.None);

        response.Assignments.Should().BeEmpty();
        response.Status.Should().Be("Unassigned");
        SavedAudit.First().Details.Should().Contain(Staff.Username);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_AssigneeAccountDeleted_StillUnassigns()
    {
        var procedure = CreateProcedure(Status.Assigned);
        var deletedUserId = Guid.NewGuid();
        procedure.Assignments[0].UserId = deletedUserId;
        GivenProcedure(procedure);

        await Handler.Handle(new UnassignPatientProcedureCommand(procedure.Id, deletedUserId, Admin), CancellationToken.None);

        procedure.Assignments.Should().BeEmpty();
    }
}
