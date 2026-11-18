using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;

[TestFixture]
public sealed class SetPathwayTemplateArchivedHandlerTests
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
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var templateId = Guid.NewGuid();
        _repository
            .Setup(r => r.GetPathwayTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PathwayTemplate?)null);

        var act = () => _fixture.Create<SetPathwayTemplateArchivedHandler>()
            .Handle(new SetPathwayTemplateArchivedCommand(templateId, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Template '{templateId}' was not found.");
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Restore_RestoresAndSavesWithoutTouchingTheStages()
    {
        var template = CreateTemplate(TemplateName, isArchived: true, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var response = await _fixture.Create<SetPathwayTemplateArchivedHandler>()
            .Handle(new SetPathwayTemplateArchivedCommand(template.Id, false), CancellationToken.None);

        response.IsArchived.Should().BeFalse();
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, null, It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
