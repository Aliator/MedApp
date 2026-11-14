using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;

[TestFixture]
public sealed class GetAllPatientProcedureStageDefinitionsHandlerTests
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
    public async Task Handle_IncludeArchived_PassesItOnAndReportsEachDefinitionsUsage()
    {
        var used = CreateDefinition(ConsultationStage);
        var unused = CreateDefinition(ProcedureStage, isArchived: true);
        _repository.Setup(r => r.GetStageDefinitionsAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        GivenUsage(used.Id, templates: 3, procedures: 0);

        var result = await _fixture.Create<GetAllPatientProcedureStageDefinitionsHandler>()
            .Handle(new GetAllPatientProcedureStageDefinitionsQuery(IncludeArchived: true), CancellationToken.None);

        result.Select(d => (d.Name, d.TemplateCount, d.CanBeDeleted, d.IsArchived))
            .Should().Equal((ConsultationStage, 3, false, false), (ProcedureStage, 0, true, true));
    }
}
