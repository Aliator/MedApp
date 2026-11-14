using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;

[TestFixture]
public sealed class SetPatientProcedureTemplateArchivedHandlerTests
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
    public async Task Handle_Restore_RestoresAndSavesWithoutTouchingTheStages()
    {
        var template = CreateTemplate(TemplateName, isArchived: true, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var response = await _fixture.Create<SetPatientProcedureTemplateArchivedHandler>()
            .Handle(new SetPatientProcedureTemplateArchivedCommand(template.Id, false), CancellationToken.None);

        response.IsArchived.Should().BeFalse();
        _repository.Verify(r => r.UpdateStageTemplateAsync(template, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
