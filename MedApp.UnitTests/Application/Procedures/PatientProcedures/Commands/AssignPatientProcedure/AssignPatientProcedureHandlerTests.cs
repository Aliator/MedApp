using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

[TestFixture]
public sealed class AssignPatientProcedureHandlerTests : ProcedureHandlerTestBase
{
    private AssignPatientProcedureHandler Handler => Fixture.Create<AssignPatientProcedureHandler>();

    [Test]
    public async Task Handle_UserDoesNotExist_ThrowsNotFound()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Unassigned));

        var act = () => Handler.Handle(new AssignPatientProcedureCommand(procedure.Id, Guid.NewGuid(), Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_AlreadyAssigned_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var act = () => Handler.Handle(new AssignPatientProcedureCommand(procedure.Id, Staff.UserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{Staff.Username} is already assigned*");
    }

    [Test]
    public async Task Handle_ProcedureCompleted_ThrowsConflict()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Completed, finishedStages: 3));

        var act = () => Handler.Handle(new AssignPatientProcedureCommand(procedure.Id, OtherUserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_NewAssignee_AssignsThemAndSaves()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Unassigned));
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new AssignPatientProcedureCommand(procedure.Id, OtherUserId, Admin), cts.Token);

        response.Status.Should().Be("Assigned");
        response.Assignments.Should().ContainSingle(a => a.Username == OtherUsername && a.AssignedByUsername == Admin.Username);
        SavedAudit.Select(e => e.Action).Should().Contain(PatientProcedureAuditAction.Assigned);
        VerifySavedOnce(cts.Token);
    }
}
