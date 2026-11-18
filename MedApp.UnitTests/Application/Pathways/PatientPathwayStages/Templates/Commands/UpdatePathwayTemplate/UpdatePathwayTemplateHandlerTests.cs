using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using MedApp.Application.Patients.Common;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

[TestFixture]
public sealed class UpdatePathwayTemplateHandlerTests
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
        GivenSpecialties();
    }

    private void GivenSpecialties(params Specialty[] specialties) =>
        _fixture.Inject(RepositoryWith(specialties).Object);

    private PathwayTemplate GivenTemplateIn(Specialty specialty)
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        template.SpecialtyId = specialty.Id;
        template.Specialty = specialty;
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        return template;
    }

    private void GivenUsage(Guid id, int templates, int pathways) =>
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage> { [id] = new(templates, pathways) });

    private void GivenDefinitions(params PatientPathwayStageDefinition[] definitions) =>
        _repository
            .Setup(r => r.GetStageDefinitionsByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<PatientPathwayStageDefinition>)definitions.Where(d => ids.Contains(d.Id)).ToList());

    private UpdatePathwayTemplateHandler Handler => _fixture.Create<UpdatePathwayTemplateHandler>();

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(Guid.NewGuid(), "x", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NewNameTaken_ThrowsConflictWithoutSaving()
    {
        const string takenName = "Taken template";
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository.Setup(r => r.PathwayTemplateNameExistsAsync(takenName, template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, $" {takenName} ", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A template called '{takenName}' already exists.");
        template.Name.Should().Be(TemplateName);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_StageArchivedAfterItWasAdded_CanStayInTheTemplate()
    {
        var consultation = CreateDefinition(ConsultationStage, isArchived: true);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> newStages = [consultation.Id, pathway.Id, consultation.Id];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, newStages), CancellationToken.None);

        response.StageDefinitionIdsInOrder.Should().Equal(newStages);
        response.Stages.Select(s => s.IsArchived).Should().Equal(true, false, true);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, newStages, It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ClockStopStage_GoesWithTheStageList()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> newStages = [consultation.Id, pathway.Id];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, newStages, 2), CancellationToken.None);

        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, newStages, 2, null, It.IsAny<CancellationToken>()), Times.Once);
        response.ClockStopStageOrder.Should().Be(2);
    }

    [Test]
    public async Task Handle_AddingAnArchivedStage_ThrowsConflict()
    {
        var archived = CreateDefinition(ConsultationStage, isArchived: true);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, pathway);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(archived, pathway);

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, [pathway.Id, archived.Id]), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_RenameOnly_KeepsTheStagesAndReportsPathwayCount()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _repository
            .Setup(r => r.GetTemplatePathwayCountsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [template.Id] = 4 });

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", null, null), CancellationToken.None);

        response.Name.Should().Be("Renamed");
        response.Stages.Should().ContainSingle(s => s.Name == ConsultationStage);
        response.PathwayCount.Should().Be(4);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, null, It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_MovedToAnotherSpecialty_SavesAndReportsTheNewOne()
    {
        var current = CreateSpecialty();
        var other = CreateSpecialty(OtherSpecialtyName);
        GivenSpecialties(current, other);
        var template = GivenTemplateIn(current);

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, other.Id, null), CancellationToken.None);

        response.SpecialtyId.Should().Be(other.Id);
        response.SpecialtyName.Should().Be(OtherSpecialtyName);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(
            It.Is<PathwayTemplate>(t => t.SpecialtyId == other.Id), null, It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_MovedToAnArchivedSpecialty_ThrowsConflictWithoutSaving()
    {
        var current = CreateSpecialty();
        var archived = CreateSpecialty(OtherSpecialtyName, isArchived: true);
        GivenSpecialties(current, archived);
        var template = GivenTemplateIn(current);

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, archived.Id, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{OtherSpecialtyName}*");
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<int?>(), It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KeepsItsArchivedSpecialty_SavesTheRename()
    {
        var archived = CreateSpecialty(isArchived: true);
        GivenSpecialties(archived);
        var template = GivenTemplateIn(archived);

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", archived.Id, null), CancellationToken.None);

        response.Name.Should().Be("Renamed");
        response.SpecialtyId.Should().Be(archived.Id);
    }

    [Test]
    public async Task Handle_UnknownSpecialty_ThrowsNotFound()
    {
        var template = GivenTemplateIn(CreateSpecialty());

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StagesWithOptions_SavesThemAndReturnsThem()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> newStages = [consultation.Id, pathway.Id, pathway.Id, consultation.Id];
        IReadOnlyList<TemplateStageOptions> options =
            [new(ChoosesBranch: true), new(Branch: "Benign"), new(IsOptional: true, Branch: " Treatment "), TemplateStageOptions.Plain];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, newStages, StageOptions: options), CancellationToken.None);

        response.Stages.Select(s => (s.IsOptional, s.ChoosesBranch, s.Branch)).Should().Equal(
            (false, true, null), (false, false, "Benign"), (true, false, "Treatment"), (false, false, null));
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, newStages, null, options, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_SameStagesWithoutOptions_KeepsTheStoredOptions()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation, pathway, pathway, consultation);
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        maps[0].ChoosesBranch = true;
        maps[1].Branch = "Benign";
        maps[2].Branch = "Treatment";
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> sameStages = [consultation.Id, pathway.Id, pathway.Id, consultation.Id];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", null, sameStages), CancellationToken.None);

        response.Stages.Select(s => (s.ChoosesBranch, s.Branch)).Should().Equal((true, null), (false, "Benign"), (false, "Treatment"), (false, null));
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, sameStages, null,
            It.Is<IReadOnlyList<TemplateStageOptions>?>(o => o != null && o[0].ChoosesBranch && o[1].Branch == "Benign"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void KeptOptions_ADifferentStageList_IsNone()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        template.Maps[0].IsOptional = true;

        UpdatePathwayTemplateHandler.KeptOptions(template, [consultation.Id, consultation.Id]).Should().BeNull();
        UpdatePathwayTemplateHandler.KeptOptions(template, null).Should().BeNull();
        UpdatePathwayTemplateHandler.KeptOptions(template, [consultation.Id])!.Single().IsOptional.Should().BeTrue();
    }

    [Test]
    public async Task Handle_RetentionClass_ChangesTheTemplate()
    {
        _fixture.Inject(Options.Create(new RetentionOptions()));
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var response = await Handler.Handle(
            new UpdatePathwayTemplateCommand(template.Id, null, null, null, RetentionClass: "MentalHealth"), CancellationToken.None);

        template.RetentionClass.Should().Be(RetentionClass.MentalHealth);
        response.RetentionClass.Should().Be("MentalHealth");
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, null, null, It.IsAny<IReadOnlyList<TemplateStageOptions>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_NoRetentionClass_LeavesIt()
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        template.RetentionClass = RetentionClass.Oncology;
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", null, null), CancellationToken.None);

        template.RetentionClass.Should().Be(RetentionClass.Oncology);
    }

    [Test]
    public async Task Handle_StandardToOncology_RaisesExistingPathwaysFromEveryShorterClass()
    {
        _fixture.Inject(Options.Create(new RetentionOptions()));
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, null, RetentionClass: "Oncology"), CancellationToken.None);

        _repository.Verify(r => r.RaisePathwayRetentionClassesAsync(
            template.Id,
            RetentionClass.Oncology,
            It.Is<IReadOnlyCollection<RetentionClass>>(from => from.Order().SequenceEqual(new[]
                { RetentionClass.Standard, RetentionClass.Maternity, RetentionClass.MentalHealth })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(RetentionClass.Oncology, "Standard")]
    [TestCase(RetentionClass.Oncology, "MentalHealth")]
    public async Task Handle_LoweringTheClass_LeavesExistingPathwaysAsTheyAre(RetentionClass before, string after)
    {
        _fixture.Inject(Options.Create(new RetentionOptions()));
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        template.RetentionClass = before;
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, null, RetentionClass: after), CancellationToken.None);

        // Oncology pathways stay Oncology: only classes shorter than the new one could be raised, and Oncology isn't.
        template.RetentionClass.ToString().Should().Be(after);
        _repository.Verify(r => r.RaisePathwayRetentionClassesAsync(
            It.IsAny<Guid>(), It.IsAny<RetentionClass>(),
            It.Is<IReadOnlyCollection<RetentionClass>>(from => from.Contains(RetentionClass.Oncology)),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
