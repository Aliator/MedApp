using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

[TestFixture]
public sealed class GetPatientProcedureByIdHandlerTests : ProcedureHandlerTestBase
{
    [Test]
    public async Task Handle_ProcedureDoesNotExist_ThrowsNotFound()
    {
        var act = () => Fixture.Create<GetPatientProcedureByIdHandler>()
            .Handle(new GetPatientProcedureByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ProcedureExists_ReturnsItWithUsernames()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.Assigned));

        var response = await Fixture.Create<GetPatientProcedureByIdHandler>()
            .Handle(new GetPatientProcedureByIdQuery(procedure.Id), CancellationToken.None);

        response.Id.Should().Be(procedure.Id);
        response.Assignments.Single().Username.Should().Be(Staff.Username);
        response.Stages.Should().HaveCount(3);
    }
}
