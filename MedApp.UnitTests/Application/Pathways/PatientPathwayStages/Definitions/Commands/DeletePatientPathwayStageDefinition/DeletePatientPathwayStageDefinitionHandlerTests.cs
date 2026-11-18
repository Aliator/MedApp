using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;

[TestFixture]
public sealed class DeletePatientPathwayStageDefinitionHandlerTests
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
        var act = () => _fixture.Create<DeletePatientPathwayStageDefinitionHandler>()
            .Handle(new DeletePatientPathwayStageDefinitionCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    public async Task Handle_DefinitionInUse_ThrowsConflictSuggestingArchiving(int templates, int pathways)
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        GivenUsage(definition.Id, templates, pathways);

        var act = () => _fixture.Create<DeletePatientPathwayStageDefinitionHandler>()
            .Handle(new DeletePatientPathwayStageDefinitionCommand(definition.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Archive it instead*");
        _repository.Verify(r => r.DeleteStageDefinitionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_DefinitionUnused_DeletesIt()
    {
        var definition = CreateDefinition(ConsultationStage);
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        using var cts = new CancellationTokenSource();

        await _fixture.Create<DeletePatientPathwayStageDefinitionHandler>()
            .Handle(new DeletePatientPathwayStageDefinitionCommand(definition.Id), cts.Token);

        _repository.Verify(r => r.DeleteStageDefinitionAsync(definition.Id, cts.Token), Times.Once);
    }
}
