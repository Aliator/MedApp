using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;

[TestFixture]
public sealed class GetPatientProcedureStageDefinitionByIdHandlerTests
{
    private Mock<IPatientProcedureStagesRepository> _repository = null!;
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientProcedureStagesRepository>();
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _fixture.Inject(_repository.Object);
    }

    [Test]
    public async Task Handle_DefinitionDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<GetPatientProcedureStageDefinitionByIdHandler>()
            .Handle(new GetPatientProcedureStageDefinitionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_DefinitionExists_ReturnsItWithItsUsage()
    {
        var definition = CreateDefinition(ConsultationStage, isArchived: true);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [definition.Id] = new(2, 7) });

        var response = await _fixture.Create<GetPatientProcedureStageDefinitionByIdHandler>()
            .Handle(new GetPatientProcedureStageDefinitionByIdQuery(definition.Id), CancellationToken.None);

        response.Name.Should().Be(ConsultationStage);
        response.Instructions.Should().Be(definition.Instructions);
        response.IsArchived.Should().BeTrue();
        response.TemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
    }
}
