using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates;

[TestFixture]
public sealed class PathwayTemplateMapperTests
{
    [TestCase(RetentionClass.Standard, "Standard")]
    [TestCase(RetentionClass.MentalHealth, "MentalHealth")]
    public void ToResponse_RetentionClass_ByName(RetentionClass retentionClass, string expected)
    {
        var template = new PathwayTemplate { Id = Guid.NewGuid(), Name = TemplateName, RetentionClass = retentionClass };

        PathwayTemplateMapper.ToResponse(template, pathwayCount: 0).RetentionClass.Should().Be(expected);
    }

    [Test]
    public void ToResponse_NoDefinitionsGiven_NamesStagesFromEachMapsDefinition()
    {
        var consultation = CreateDefinition(ConsultationStage, isArchived: true);
        var template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(PathwayStage));

        var response = PathwayTemplateMapper.ToResponse(template, pathwayCount: 2);

        response.Name.Should().Be(TemplateName);
        response.PathwayCount.Should().Be(2);
        response.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, PathwayStage);
        response.Stages.Select(s => s.IsArchived).Should().Equal(true, false);
        response.Stages[0].Description.Should().Be($"{ConsultationStage} description");
    }

    [Test]
    public void ToResponse_DefinitionsGiven_TakeTheNamesFromThemOverTheMaps()
    {
        var template = new PathwayTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        var definition = CreateDefinition(ConsultationStage);
        template.Maps = TemplateShape.ToMaps(template.Id, TeamId, [definition.Id], null, null);

        var response = PathwayTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientPathwayStageDefinition>
        {
            [definition.Id] = definition
        });

        response.Stages.Should().ContainSingle().Which.Name.Should().Be(ConsultationStage);
    }

    [Test]
    public void ToResponse_DefinitionMissingFromTheLookupAndTheMap_ShowsABlankActiveStage()
    {
        var template = new PathwayTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        template.Maps = TemplateShape.ToMaps(template.Id, TeamId, [ConsultationDefinitionId], null, null);
        template.Maps[0].StageDefinition = null!;

        var response = PathwayTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientPathwayStageDefinition>());

        var stage = response.Stages.Should().ContainSingle().Subject;
        stage.StageDefinitionId.Should().Be(ConsultationDefinitionId);
        stage.StageOrder.Should().Be(1);
        stage.Name.Should().BeEmpty();
        stage.Description.Should().BeEmpty();
        stage.IsArchived.Should().BeFalse();
        response.StageDefinitionIdsInOrder.Should().Equal(ConsultationDefinitionId);
    }

    [Test]
    public void ToMaps_RepeatedStages_NumbersThemFromOneInOrder()
    {
        var templateId = Guid.NewGuid();

        var maps = TemplateShape.ToMaps(templateId, TeamId, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId], null, null);

        maps.Select(m => m.StageOrder).Should().Equal(1, 2, 3);
        maps.Select(m => m.StageDefinitionId).Should().Equal(ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId);
        maps.Should().OnlyContain(m => m.TemplateId == templateId);
        maps.Should().OnlyContain(m => !m.StopsClock);
    }

    [Test]
    public void ToMaps_ClockStopStage_MarksThatPositionAndTheResponseGivesIt()
    {
        var template = new MedApp.Domain.Pathways.PatientPathways.PathwayTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        template.Maps = TemplateShape.ToMaps(template.Id, TeamId, [ConsultationDefinitionId, PathwayDefinitionId], 2, null);

        template.Maps.Select(m => m.StopsClock).Should().Equal(false, true);
        var response = PathwayTemplateMapper.ToResponse(template, 0);
        response.ClockStopStageOrder.Should().Be(2);
        response.Stages.Select(s => s.StopsClock).Should().Equal(false, true);
        PathwayTemplateMapper.ToResponse(new MedApp.Domain.Pathways.PatientPathways.PathwayTemplate(), 0).ClockStopStageOrder.Should().BeNull();
    }

    [Test]
    public void ToResponse_DefinitionsWithVisits_GiveEachStageItsVisitRequirement()
    {
        var template = CreateTemplate(
            TemplateName,
            false,
            CreateDefinition(ConsultationStage).WithVisit(VisitRequirement.Required),
            CreateDefinition(PathwayStage));

        var response = PathwayTemplateMapper.ToResponse(template, pathwayCount: 0);

        response.Stages.Select(s => s.VisitRequirement).Should().Equal("Required", "None");
    }

    [Test]
    public void ToResponse_DefinitionMissing_HasNoVisitRequirement()
    {
        var template = new PathwayTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        template.Maps = TemplateShape.ToMaps(template.Id, TeamId, [ConsultationDefinitionId], null, null);
        template.Maps[0].StageDefinition = null!;

        var response = PathwayTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientPathwayStageDefinition>());

        response.Stages.Single().VisitRequirement.Should().Be("None");
    }

    [Test]
    public void ToMaps_WithOptions_CopiesThemTheTemplatesOwnTeamAsNone()
    {
        var lab = Guid.NewGuid();
        var maps = TemplateShape.ToMaps(Guid.NewGuid(), TeamId, [ConsultationDefinitionId, PathwayDefinitionId, PathwayDefinitionId], null,
        [
            new TemplateStageOptions(ChoiceKind: "assessment", Outcomes: [new PathwayTemplateOutcome(" Benign ", [3, 2, 2])]),
            new TemplateStageOptions(IsOptional: true, OwnerTeamId: lab),
            new TemplateStageOptions(OwnerTeamId: TeamId, Outcomes: [new PathwayTemplateOutcome("Ignored", [3])])
        ]);

        maps.Select(m => (m.IsOptional, m.ChoiceKind, m.OwnerTeamId)).Should().Equal(
            (false, (StageChoiceKind?)StageChoiceKind.Assessment, (Guid?)null), (true, null, lab), (false, null, null));
        maps[0].Outcomes.Should().ContainSingle().Which.Should().BeEquivalentTo(new PathwayTemplateOutcome("Benign", [2, 3]));
        maps[2].Outcomes.Should().BeEmpty();
    }

    [Test]
    public void ToMaps_WithoutOptions_MakesPlainStages()
    {
        var maps = TemplateShape.ToMaps(Guid.NewGuid(), TeamId, [ConsultationDefinitionId, PathwayDefinitionId], null, null);

        maps.Should().AllSatisfy(m => (m.IsOptional, m.ChoiceKind, m.OwnerTeamId).Should().Be((false, (StageChoiceKind?)null, (Guid?)null)));
    }

    [Test]
    public void ToResponse_RoutesOwnersOutcomesAndBranches_AreGiven_AndAListShowsItsChain()
    {
        var lab = Guid.NewGuid();
        var definition = CreateDefinition(PathwayStage);
        var branch = new PathwayTemplateBranch(Guid.NewGuid(), "Repeat", [definition.Id], [1], true);
        var template = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage), CreateDefinition(PathwayStage), CreateDefinition(PathwayStage));
        template.Maps[0].ChoiceKind = StageChoiceKind.LabResult;
        template.Maps[0].Outcomes = [new PathwayTemplateOutcome("Normal", [2]), new PathwayTemplateOutcome("Abnormal", [3])];
        template.Maps[1].OwnerTeamId = lab;
        template.Links = [new(1, 2), new(1, 3)];
        template.AddableBranches = [branch];

        var response = PathwayTemplateMapper.ToResponse(
            template, 0, new Dictionary<Guid, PatientPathwayStageDefinition> { [definition.Id] = definition },
            new Dictionary<Guid, string> { [lab] = "Lab" });

        response.Stages[0].ChoiceKind.Should().Be("LabResult");
        response.Stages[0].Outcomes!.Select(o => o.Name).Should().Equal("Normal", "Abnormal");
        response.Stages[1].Outcomes.Should().BeNull();
        response.Stages[1].OwnerTeamName.Should().Be("Lab");
        response.Links.Select(l => (l.FromStageOrder, l.ToStageOrder)).Should().Equal((1, 2), (1, 3));
        response.AddableBranches.Single().Stages.Single().Name.Should().Be(PathwayStage);
        response.AddableBranches.Single().AttachAfterStageOrders.Should().Equal(1);

        var list = CreateTemplate(TemplateName, false, CreateDefinition(ConsultationStage), CreateDefinition(PathwayStage));
        PathwayTemplateMapper.ToResponse(list, 0).Links.Select(l => (l.FromStageOrder, l.ToStageOrder)).Should().Equal((1, 2));
    }
}
