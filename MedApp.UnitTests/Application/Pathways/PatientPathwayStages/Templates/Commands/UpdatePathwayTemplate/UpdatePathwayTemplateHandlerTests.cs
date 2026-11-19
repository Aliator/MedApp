using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using MedApp.Application.Patients.Common;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.TeamsTestConstants;
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
        GivenTeams();
    }

    private void GivenTeams(params Team[] teams) =>
        _fixture.Inject(RepositoryWith(teams).Object);

    private PathwayTemplate GivenTemplateIn(Team team)
    {
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage));
        template.TeamId = team.Id;
        template.Team = team;
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
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, It.Is<IReadOnlyList<PathwayTemplateMap>?>(m => m!.Select(x => x.StageDefinitionId).SequenceEqual(newStages)), It.IsAny<CancellationToken>()), Times.Once);
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

        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, It.Is<IReadOnlyList<PathwayTemplateMap>?>(m => m!.Select(x => x.StopsClock).SequenceEqual(new[] { false, true })), It.IsAny<CancellationToken>()), Times.Once);
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
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_MovedToAnotherTeam_SavesAndReportsTheNewOne()
    {
        var current = CreateTeam();
        var other = CreateTeam(OtherTeamName);
        GivenTeams(current, other);
        var template = GivenTemplateIn(current);

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, other.Id, null), CancellationToken.None);

        response.TeamId.Should().Be(other.Id);
        response.TeamName.Should().Be(OtherTeamName);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(
            It.Is<PathwayTemplate>(t => t.TeamId == other.Id), It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_MovedToAnArchivedTeam_ThrowsConflictWithoutSaving()
    {
        var current = CreateTeam();
        var archived = CreateTeam(OtherTeamName, isArchived: true);
        GivenTeams(current, archived);
        var template = GivenTemplateIn(current);

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, archived.Id, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{OtherTeamName}*");
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(It.IsAny<PathwayTemplate>(), It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_KeepsItsArchivedTeam_SavesTheRename()
    {
        var archived = CreateTeam(isArchived: true);
        GivenTeams(archived);
        var template = GivenTemplateIn(archived);

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", archived.Id, null), CancellationToken.None);

        response.Name.Should().Be("Renamed");
        response.TeamId.Should().Be(archived.Id);
    }

    [Test]
    public async Task Handle_UnknownTeam_ThrowsNotFound()
    {
        var template = GivenTemplateIn(CreateTeam());

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, Guid.NewGuid(), null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StagesWithAChoiceNodeAndLinks_SavesThemAndReturnsThem()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> newStages = [consultation.Id, pathway.Id, pathway.Id];
        IReadOnlyList<TemplateStageOptions> options =
        [
            new(ChoiceKind: "Assessment", Outcomes: [new PathwayTemplateOutcome("Benign", [2]), new PathwayTemplateOutcome("Treatment", [3])]),
            TemplateStageOptions.Plain,
            TemplateStageOptions.Plain
        ];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(
            template.Id, null, null, newStages, StageOptions: options, Links: [new(1, 2), new(1, 3)]), CancellationToken.None);

        response.Stages.Select(s => s.ChoiceKind).Should().Equal("Assessment", null, null);
        response.Stages[0].Outcomes!.Select(o => o.Name).Should().Equal("Benign", "Treatment");
        response.Links.Select(l => (l.FromStageOrder, l.ToStageOrder)).Should().Equal((1, 2), (1, 3));
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(
            It.Is<PathwayTemplate>(t => t.Links.Count == 2),
            It.Is<IReadOnlyList<PathwayTemplateMap>?>(m => m![0].ChoiceKind == StageChoiceKind.Assessment),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AChoiceNodeWithoutLinks_IsRefusedOnLinks()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation);

        var act = () => Handler.Handle(new UpdatePathwayTemplateCommand(
            template.Id, null, null, [consultation.Id, consultation.Id],
            StageOptions: [new(ChoiceKind: "Assessment"), TemplateStageOptions.Plain]), CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>()).Which.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Links" && e.ErrorMessage == TemplateShape.ChoiceNeedsLinksMessage);
    }

    [Test]
    public async Task Handle_ATemplateWithRoutesChangedAsAList_IsRefused_ButItsNameChanges()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation, pathway, pathway);
        template.Links = [new(1, 2), new(1, 3)];
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);

        var asAList = () => Handler.Handle(new UpdatePathwayTemplateCommand(
            template.Id, null, null, [consultation.Id, pathway.Id]), CancellationToken.None);
        var renamed = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", null, null), CancellationToken.None);

        (await asAList.Should().ThrowAsync<FluentValidation.ValidationException>()).Which.Errors.Should().ContainSingle(e =>
            e.PropertyName == "StageDefinitionIdsInOrder" && e.ErrorMessage == TemplateShape.ParallelRoutesLockedMessage);
        renamed.Links.Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_AStageOwnedByAnotherClinic_IsRefused_AndTheLabIsAllowed()
    {
        var lab = CreateTeam("Lab");
        var current = CreateTeam();
        GivenTeams(current, lab);
        _fixture.Inject(TeamsWithLab(lab, current).Object);
        var consultation = CreateDefinition(ConsultationStage);
        var template = GivenTemplateIn(current);
        GivenDefinitions(consultation, template.Maps[0].StageDefinition);
        IReadOnlyList<Guid> stages = [template.Maps[0].StageDefinitionId, consultation.Id];

        var other = () => Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, stages,
            StageOptions: [TemplateStageOptions.Plain, new(OwnerTeamId: Guid.NewGuid())]), CancellationToken.None);
        var labs = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, null, null, stages,
            StageOptions: [TemplateStageOptions.Plain, new(OwnerTeamId: lab.Id)]), CancellationToken.None);

        (await other.Should().ThrowAsync<FluentValidation.ValidationException>()).Which.Errors.Should().ContainSingle(e =>
            e.ErrorMessage == TemplateShape.OwnerMessage);
        labs.Stages[1].OwnerTeamId.Should().Be(lab.Id);
        labs.Stages[1].OwnerTeamName.Should().Be("Lab");
    }

    private static Mock<MedApp.Application.Teams.Repositories.ITeamRepository> TeamsWithLab(Team lab, params Team[] others)
    {
        var teams = RepositoryWith([lab, .. others]);
        teams.Setup(r => r.GetLabAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lab);
        return teams;
    }

    [Test]
    public async Task Handle_SameStagesWithoutOptions_KeepsTheStoredOptions()
    {
        var consultation = CreateDefinition(ConsultationStage);
        var pathway = CreateDefinition(PathwayStage);
        var template = CreateTemplate(TemplateName, false, consultation, pathway, consultation);
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        maps[1].IsOptional = true;
        _repository.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        GivenDefinitions(consultation, pathway);
        IReadOnlyList<Guid> sameStages = [consultation.Id, pathway.Id, consultation.Id];

        var response = await Handler.Handle(new UpdatePathwayTemplateCommand(template.Id, "Renamed", null, sameStages), CancellationToken.None);

        response.Stages.Select(s => s.IsOptional).Should().Equal(false, true, false);
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template,
            It.Is<IReadOnlyList<PathwayTemplateMap>?>(m => m![1].IsOptional), It.IsAny<CancellationToken>()), Times.Once);
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
        _repository.Verify(r => r.UpdatePathwayTemplateAsync(template, It.IsAny<IReadOnlyList<PathwayTemplateMap>?>(), It.IsAny<CancellationToken>()), Times.Once);
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
