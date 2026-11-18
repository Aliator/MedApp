using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;

[TestFixture]
public sealed class UpdatePatientPathwayStageDefinitionHandlerTests
{
    private Mock<IPatientPathwayStagesRepository> _repository = null!;
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientPathwayStagesRepository>();
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _fixture.Inject(_repository.Object);
    }

    private void GivenUsage(Guid id, int templates, int pathways) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, pathways) });

    [Test]
    public async Task Handle_DefinitionDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(Guid.NewGuid(), "x", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NewNameTaken_ThrowsConflict()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        _repository.Setup(r => r.StageDefinitionNameExistsAsync(PathwayStage, definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, PathwayStage, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A stage called '{PathwayStage}' already exists.");
        _repository.Verify(r => r.UpdateStageDefinitionAsync(It.IsAny<PatientPathwayStageDefinition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_NewNameFree_RenamesTrimmedAndUpdatesInstructions()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        _repository.Setup(r => r.StageDefinitionNameExistsAsync(PathwayStage, definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var response = await _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, $" {PathwayStage} ", null, "New instructions"), CancellationToken.None);

        definition.Name.Should().Be(PathwayStage);
        definition.Instructions.Should().Be("New instructions");
        definition.Description.Should().Be($"{ConsultationStage} description");
        response.Name.Should().Be(PathwayStage);
        response.Instructions.Should().Be("New instructions");
        _repository.Verify(r => r.UpdateStageDefinitionAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_InstructionsNull_LeavesThemUnchanged()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);

        var response = await _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, ConsultationStage, "New description", null), CancellationToken.None);

        response.Instructions.Should().Be($"{ConsultationStage} instructions");
        _repository.Verify(r => r.StageDefinitionNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_FieldsGiven_UpdatesThemAndReportsUsage()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        GivenUsage(definition.Id, templates: 2, pathways: 5);

        var response = await _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, null, "New description", null), CancellationToken.None);

        response.Description.Should().Be("New description");
        response.Name.Should().Be(ConsultationStage);
        response.TemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        _repository.Verify(r => r.UpdateStageDefinitionAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_VisitRequirementGiven_ChangesIt()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);

        var response = await _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, null, null, null, VisitRequirement: "required"), CancellationToken.None);

        definition.VisitRequirement.Should().Be(VisitRequirement.Required);
        response.VisitRequirement.Should().Be("Required");
        _repository.Verify(r => r.UpdateStageDefinitionAsync(definition, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_VisitRequirementNull_LeavesItUnchanged()
    {
        var definition = CreateDefinition(ConsultationStage).WithVisit(VisitRequirement.Suggested);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);

        var response = await _fixture.Create<UpdatePatientPathwayStageDefinitionHandler>()
            .Handle(new UpdatePatientPathwayStageDefinitionCommand(definition.Id, null, "New description", null), CancellationToken.None);

        definition.VisitRequirement.Should().Be(VisitRequirement.Suggested);
        response.VisitRequirement.Should().Be("Suggested");
    }
}
