using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;

[TestFixture]
public sealed class GetPatientProcedureTemplateByIdHandlerTests
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

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => _fixture.Create<GetPatientProcedureTemplateByIdHandler>()
            .Handle(new GetPatientProcedureTemplateByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_TemplateExists_ReturnsItsStagesInOrderAndHowOftenItWasUsed()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(ProcedureStage), consultation);
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 5 });

        var response = await _fixture.Create<GetPatientProcedureTemplateByIdHandler>()
            .Handle(new GetPatientProcedureTemplateByIdQuery(template.Id), CancellationToken.None);

        response.Stages.Select(s => (s.StageOrder, s.Name)).Should().Equal((1, ConsultationStage), (2, ProcedureStage), (3, ConsultationStage));
        response.StageDefinitionIdsInOrder.Should().HaveCount(3);
        response.ProcedureCount.Should().Be(5);
    }
}
