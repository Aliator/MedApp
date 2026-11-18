using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;

[TestFixture]
public sealed class DeletePathwayTemplateHandlerTests
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
    public async Task Handle_TemplateUsedByPathways_ThrowsConflictSuggestingArchiving()
    {
        var template = CreateTemplate();
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 2 });

        var act = () => _fixture.Create<DeletePathwayTemplateHandler>()
            .Handle(new DeletePathwayTemplateCommand(template.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Archive it instead*");
    }

    [Test]
    public async Task Handle_TemplateUnused_DeletesIt()
    {
        var template = CreateTemplate();
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        using var cts = new CancellationTokenSource();

        await _fixture.Create<DeletePathwayTemplateHandler>()
            .Handle(new DeletePathwayTemplateCommand(template.Id), cts.Token);

        _repository.Verify(r => r.DeletePathwayTemplateAsync(template.Id, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<DeletePathwayTemplateHandler>()
            .Handle(new DeletePathwayTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
