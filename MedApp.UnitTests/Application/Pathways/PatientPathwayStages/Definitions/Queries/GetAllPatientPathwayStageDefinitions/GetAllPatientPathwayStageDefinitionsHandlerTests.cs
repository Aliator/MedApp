using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;

[TestFixture]
public sealed class GetAllPatientPathwayStageDefinitionsHandlerTests
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
    public async Task Handle_IncludeArchived_PassesItOnAndReportsEachDefinitionsUsage()
    {
        var used = CreateDefinition(ConsultationStage);
        var unused = CreateDefinition(PathwayStage, isArchived: true);
        _repository.Setup(r => r.GetStageDefinitionsAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        GivenUsage(used.Id, templates: 3, pathways: 0);

        var result = await _fixture.Create<GetAllPatientPathwayStageDefinitionsHandler>()
            .Handle(new GetAllPatientPathwayStageDefinitionsQuery(IncludeArchived: true), CancellationToken.None);

        result.Select(d => (d.Name, d.TemplateCount, d.CanBeDeleted, d.IsArchived))
            .Should().Equal((ConsultationStage, 3, false, false), (PathwayStage, 0, true, true));
    }
}
