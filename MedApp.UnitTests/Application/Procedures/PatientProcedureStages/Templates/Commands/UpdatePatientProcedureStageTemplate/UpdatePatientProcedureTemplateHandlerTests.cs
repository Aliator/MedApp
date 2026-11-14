using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

[TestFixture]
public sealed class UpdatePatientProcedureTemplateHandlerTests
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

    private void GivenDefinitions(params PatientProcedureStageDefinition[] definitions) =>
        _repository
            .Setup(r => r.GetStageDefinitionsByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<PatientProcedureStageDefinition>)definitions.Where(d => ids.Contains(d.Id)).ToList());

    private UpdatePatientProcedureTemplateHandler Handler => _fixture.Create<UpdatePatientProcedureTemplateHandler>();

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), "x", null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StageArchivedAfterItWasAdded_CanStayInTheTemplate()
    {
        var consultation = CreateDefinition(ConsultationStage, isArchived: true);
        var procedure = CreateDefinition(ProcedureStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, procedure);
        IReadOnlyList<Guid> newStages = [consultation.Id, procedure.Id, consultation.Id];

        var response = await Handler.Handle(new UpdatePatientProcedureTemplateCommand(template.Id, null, newStages), CancellationToken.None);

        response.StageDefinitionIdsInOrder.Should().Equal(newStages);
        response.Stages.Select(s => s.IsArchived).Should().Equal(true, false, true);
        _repository.Verify(r => r.UpdateStageTemplateAsync(template, newStages, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AddingAnArchivedStage_ThrowsConflict()
    {
        var archived = CreateDefinition(ConsultationStage, isArchived: true);
        var procedure = CreateDefinition(ProcedureStage);
        var template = CreateTemplate(TemplateName, false, procedure);
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(archived, procedure);

        var act = () => Handler.Handle(new UpdatePatientProcedureTemplateCommand(template.Id, null, [procedure.Id, archived.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.UpdateStageTemplateAsync(It.IsAny<PatientProcedureStageTemplate>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_RenameOnly_KeepsTheStagesAndReportsProcedureCount()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplateProcedureCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 4 });

        var response = await Handler.Handle(new UpdatePatientProcedureTemplateCommand(template.Id, "Renamed", null), CancellationToken.None);

        response.Name.Should().Be("Renamed");
        response.Stages.Should().ContainSingle(s => s.Name == ConsultationStage);
        response.ProcedureCount.Should().Be(4);
        _repository.Verify(r => r.UpdateStageTemplateAsync(template, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
