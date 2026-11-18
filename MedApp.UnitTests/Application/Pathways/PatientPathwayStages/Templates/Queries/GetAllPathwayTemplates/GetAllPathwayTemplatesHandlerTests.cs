using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;

[TestFixture]
public sealed class GetAllPathwayTemplatesHandlerTests
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_IncludeArchived_PassesItOnAndReportsEachTemplatesUse(bool includeArchived)
    {
        var used = CreateTemplate("Used", false, CreateDefinition(ConsultationStage));
        var unused = CreateTemplate("Unused", true, CreateDefinition(PathwayStage));
        _repository.Setup(r => r.GetPathwayTemplatesAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [used.Id] = 3 });

        var result = await _fixture.Create<GetAllPathwayTemplatesHandler>()
            .Handle(new GetAllPathwayTemplatesQuery(includeArchived), CancellationToken.None);

        result.Select(t => (t.Name, t.PathwayCount, t.IsArchived)).Should().Equal(("Used", 3, false), ("Unused", 0, true));
        _repository.Verify(r => r.GetPathwayTemplatesAsync(includeArchived, It.IsAny<CancellationToken>()), Times.Once);
    }
}
