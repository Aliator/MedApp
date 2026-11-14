using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

[TestFixture]
public sealed class UpdatePatientProcedureStageDefinitionHandlerTests
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
        var act = () => _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>()
            .Handle(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), "x", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NewNameTaken_ThrowsConflict()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        _repository.Setup(r => r.StageDefinitionNameExistsAsync(ProcedureStage, definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>()
            .Handle(new UpdatePatientProcedureStageDefinitionCommand(definition.Id, ProcedureStage, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_FieldsGiven_UpdatesThemAndReportsUsage()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        GivenUsage(definition.Id, templates: 2, procedures: 5);

        var response = await _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>()
            .Handle(new UpdatePatientProcedureStageDefinitionCommand(definition.Id, null, "New description", null), CancellationToken.None);

        response.Description.Should().Be("New description");
        response.Name.Should().Be(ConsultationStage);
        response.TemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        _repository.Verify(r => r.UpdateStageDefinitionAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
    }
}
