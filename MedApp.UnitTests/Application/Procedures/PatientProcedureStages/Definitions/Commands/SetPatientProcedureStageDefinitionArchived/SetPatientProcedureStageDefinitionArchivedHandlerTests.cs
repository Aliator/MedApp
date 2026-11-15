using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;

[TestFixture]
public sealed class SetPatientProcedureStageDefinitionArchivedHandlerTests
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

    private void GivenUsage(Guid id, int templates, int procedures) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, procedures) });

    [Test]
    public async Task Handle_DefinitionDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<SetPatientProcedureStageDefinitionArchivedHandler>()
            .Handle(new SetPatientProcedureStageDefinitionArchivedCommand(Guid.NewGuid(), true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Archive_ArchivesAndSaves()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);

        var response = await _fixture.Create<SetPatientProcedureStageDefinitionArchivedHandler>()
            .Handle(new SetPatientProcedureStageDefinitionArchivedCommand(definition.Id, true), CancellationToken.None);

        response.IsArchived.Should().BeTrue();
        _repository.Verify(r => r.UpdateStageDefinitionAsync(It.Is<PatientProcedureStageDefinition>(d => d.IsArchived), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AlreadyInThatState_DoesNotSave()
    {
        var definition = CreateDefinition(ConsultationStage, isArchived: true);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);

        await _fixture.Create<SetPatientProcedureStageDefinitionArchivedHandler>()
            .Handle(new SetPatientProcedureStageDefinitionArchivedCommand(definition.Id, true), CancellationToken.None);

        _repository.Verify(r => r.UpdateStageDefinitionAsync(It.IsAny<PatientProcedureStageDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
