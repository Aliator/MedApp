using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;

[TestFixture]
public sealed class GetAllPatientProcedureTemplatesHandlerTests
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_IncludeArchived_PassesItOnAndReportsEachTemplatesUse(bool includeArchived)
    {
        var used = CreateTemplate("Used", false, CreateDefinition(ConsultationStage));
        var unused = CreateTemplate("Unused", true, CreateDefinition(ProcedureStage));
        _repository.Setup(r => r.GetStageTemplatesAsync(includeArchived, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [used.Id] = 3 });

        var result = await _fixture.Create<GetAllPatientProcedureTemplatesHandler>()
            .Handle(new GetAllPatientProcedureTemplatesQuery(includeArchived), CancellationToken.None);

        result.Select(t => (t.Name, t.ProcedureCount, t.IsArchived)).Should().Equal(("Used", 3, false), ("Unused", 0, true));
        _repository.Verify(r => r.GetStageTemplatesAsync(includeArchived, It.IsAny<CancellationToken>()), Times.Once);
    }
}
