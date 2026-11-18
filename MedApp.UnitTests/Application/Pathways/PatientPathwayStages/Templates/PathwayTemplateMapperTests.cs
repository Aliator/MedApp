using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates;

[TestFixture]
public sealed class PathwayTemplateMapperTests
{
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
        template.Maps = PathwayTemplateMapper.ToMaps(template.Id, [definition.Id]);

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
        template.Maps = PathwayTemplateMapper.ToMaps(template.Id, [ConsultationDefinitionId]);
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

        var maps = PathwayTemplateMapper.ToMaps(templateId, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId]);

        maps.Select(m => m.StageOrder).Should().Equal(1, 2, 3);
        maps.Select(m => m.StageDefinitionId).Should().Equal(ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId);
        maps.Should().OnlyContain(m => m.TemplateId == templateId);
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
        template.Maps = PathwayTemplateMapper.ToMaps(template.Id, [ConsultationDefinitionId]);
        template.Maps[0].StageDefinition = null!;

        var response = PathwayTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientPathwayStageDefinition>());

        response.Stages.Single().VisitRequirement.Should().Be("None");
    }

    [Test]
    public void ToMaps_WithOptions_CopiesThemTrimmingBranchNames()
    {
        var maps = PathwayTemplateMapper.ToMaps(Guid.NewGuid(), [ConsultationDefinitionId, PathwayDefinitionId],
            [new TemplateStageOptions(ChoosesBranch: true), new TemplateStageOptions(IsOptional: true, Branch: "  ")]);

        maps.Select(m => (m.IsOptional, m.ChoosesBranch, m.Branch)).Should().Equal((false, true, null), (true, false, null));
    }

    [Test]
    public void ToMaps_WithoutOptions_MakesPlainStages()
    {
        var maps = PathwayTemplateMapper.ToMaps(Guid.NewGuid(), [ConsultationDefinitionId, PathwayDefinitionId]);

        maps.Should().AllSatisfy(m => (m.IsOptional, m.ChoosesBranch, m.Branch).Should().Be((false, false, (string?)null)));
    }
}
