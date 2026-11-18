using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

[TestFixture]
public sealed class CreatePathwayTemplateHandlerTests
{
    private Mock<IPatientPathwayStagesRepository> _repository = null!;
    private Specialty _specialty = null!;
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
        _specialty = CreateSpecialty(id: SpecialtyId);
        GivenSpecialties(_specialty);
    }

    private void GivenSpecialties(params Specialty[] specialties) =>
        _fixture.Inject(RepositoryWith(specialties).Object);


    private void GivenUsage(Guid id, int templates, int pathways) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, pathways) });

    private void GivenDefinitions(params PatientPathwayStageDefinition[] definitions) =>
        _repository
            .Setup(r => r.GetStageDefinitionsByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<PatientPathwayStageDefinition>)definitions.Where(d => ids.Contains(d.Id)).ToList());

    private CreatePathwayTemplateHandler Handler => _fixture.Create<CreatePathwayTemplateHandler>();

    [Test]
    public async Task Handle_RepeatedStages_KeepsEveryPositionInOrder()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        GivenDefinitions(consultation, pathway);
        PathwayTemplate? added = null;
        _repository
            .Setup(r => r.AddPathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayTemplate t, CancellationToken _) => added = t);

        var response = await Handler.Handle(
            new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [consultation.Id, pathway.Id, consultation.Id]),
            CancellationToken.None);

        added!.Maps.Select(m => (m.StageOrder, m.StageDefinitionId))
            .Should().Equal((1, consultation.Id), (2, pathway.Id), (3, consultation.Id));
        response.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, PathwayStage, ConsultationStage);
        response.StageDefinitionIdsInOrder.Should().Equal(consultation.Id, pathway.Id, consultation.Id);
        response.PathwayCount.Should().Be(0);
        added.SpecialtyId.Should().Be(SpecialtyId);
        response.SpecialtyId.Should().Be(SpecialtyId);
        response.SpecialtyName.Should().Be(SpecialtyName);
    }

    [Test]
    public async Task Handle_ClockStopStage_MarksThatPositionOnly()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        GivenDefinitions(consultation, pathway);
        PathwayTemplate? added = null;
        _repository
            .Setup(r => r.AddPathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayTemplate t, CancellationToken _) => added = t);

        var response = await Handler.Handle(
            new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [consultation.Id, pathway.Id, consultation.Id], 3),
            CancellationToken.None);

        added!.Maps.Select(m => m.StopsClock).Should().Equal(false, false, true);
        response.ClockStopStageOrder.Should().Be(3);
        response.Stages.Select(s => s.StopsClock).Should().Equal(false, false, true);
    }

    [Test]
    public async Task Handle_UnknownSpecialty_ThrowsNotFoundWithoutSaving()
    {
        var consultation = CreateDefinition(ConsultationStage);
        GivenDefinitions(consultation);
        GivenSpecialties();

        var act = () => Handler.Handle(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [consultation.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.AddPathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ArchivedSpecialty_ThrowsConflictWithoutSaving()
    {
        var consultation = CreateDefinition(ConsultationStage);
        GivenDefinitions(consultation);
        GivenSpecialties(CreateSpecialty(isArchived: true, id: SpecialtyId));

        var act = () => Handler.Handle(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [consultation.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{SpecialtyName}*");
        _repository.Verify(r => r.AddPathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownStage_ThrowsNotFound()
    {
        GivenDefinitions();

        var act = () => Handler.Handle(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ArchivedStage_ThrowsConflict()
    {
        var archived = CreateDefinition(ConsultationStage, isArchived: true);
        GivenDefinitions(archived);

        var act = () => Handler.Handle(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [archived.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{ConsultationStage}*");
    }

    [Test]
    public async Task Handle_NameAlreadyUsed_ThrowsConflict()
    {
        _repository.Setup(r => r.PathwayTemplateNameExistsAsync(TemplateName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [Guid.NewGuid()]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
