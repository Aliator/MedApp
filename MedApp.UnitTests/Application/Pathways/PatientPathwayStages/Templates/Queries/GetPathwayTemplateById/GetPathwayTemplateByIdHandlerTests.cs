using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;

[TestFixture]
public sealed class GetPathwayTemplateByIdHandlerTests
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

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<GetPathwayTemplateByIdHandler>()
            .Handle(new GetPathwayTemplateByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_TemplateExists_ReturnsItsStagesInOrderAndHowOftenItWasUsed()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(PathwayStage), consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 5 });

        var response = await _fixture.Create<GetPathwayTemplateByIdHandler>()
            .Handle(new GetPathwayTemplateByIdQuery(template.Id), CancellationToken.None);

        response.Stages.Select(s => (s.StageOrder, s.Name)).Should().Equal((1, ConsultationStage), (2, PathwayStage), (3, ConsultationStage));
        response.StageDefinitionIdsInOrder.Should().HaveCount(3);
        response.PathwayCount.Should().Be(5);
    }
}
