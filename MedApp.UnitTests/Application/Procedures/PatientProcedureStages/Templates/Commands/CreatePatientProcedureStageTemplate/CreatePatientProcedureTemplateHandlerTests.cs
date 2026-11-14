using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

[TestFixture]
public sealed class CreatePatientProcedureTemplateHandlerTests
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

    private CreatePatientProcedureTemplateHandler Handler => _fixture.Create<CreatePatientProcedureTemplateHandler>();

    [Test]
    public async Task Handle_RepeatedStages_KeepsEveryPositionInOrder()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var procedure = CreateDefinition(ProcedureStage);
        GivenDefinitions(consultation, procedure);
        PatientProcedureStageTemplate? added = null;
        _repository
            .Setup(r => r.AddStageTemplateAsync(It.IsAny<PatientProcedureStageTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((PatientProcedureStageTemplate t, CancellationToken _) => added = t);

        var response = await Handler.Handle(
            new CreatePatientProcedureTemplateCommand(TemplateName, [consultation.Id, procedure.Id, consultation.Id]),
            CancellationToken.None);

        added!.Maps.Select(m => (m.StageOrder, m.StageDefinitionId))
            .Should().Equal((1, consultation.Id), (2, procedure.Id), (3, consultation.Id));
        response.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, ProcedureStage, ConsultationStage);
        response.StageDefinitionIdsInOrder.Should().Equal(consultation.Id, procedure.Id, consultation.Id);
        response.ProcedureCount.Should().Be(0);
    }

    [Test]
    public async Task Handle_UnknownStage_ThrowsNotFound()
    {
        GivenDefinitions();

        var act = () => Handler.Handle(new CreatePatientProcedureTemplateCommand(TemplateName, [Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ArchivedStage_ThrowsConflict()
    {
        var archived = CreateDefinition(ConsultationStage, isArchived: true);
        GivenDefinitions(archived);

        var act = () => Handler.Handle(new CreatePatientProcedureTemplateCommand(TemplateName, [archived.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{ConsultationStage}*");
    }

    [Test]
    public async Task Handle_NameAlreadyUsed_ThrowsConflict()
    {
        _repository.Setup(r => r.StageTemplateNameExistsAsync(TemplateName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new CreatePatientProcedureTemplateCommand(TemplateName, [Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
