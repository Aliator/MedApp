using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Templates;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates;

[TestFixture]
public sealed class StageTemplateMapperTests
{
    [Test]
    public void ToResponse_NoDefinitionsGiven_NamesStagesFromEachMapsDefinition()
    {
        var consultation = CreateDefinition(ConsultationStage, isArchived: true);
        var template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(ProcedureStage));

        var response = StageTemplateMapper.ToResponse(template, procedureCount: 2);

        response.Name.Should().Be(TemplateName);
        response.ProcedureCount.Should().Be(2);
        response.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, ProcedureStage);
        response.Stages.Select(s => s.IsArchived).Should().Equal(true, false);
        response.Stages[0].Description.Should().Be($"{ConsultationStage} description");
    }

    [Test]
    public void ToResponse_DefinitionsGiven_TakeTheNamesFromThemOverTheMaps()
    {
        var template = new PatientProcedureStageTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        var definition = CreateDefinition(ConsultationStage);
        template.Maps = StageTemplateMapper.ToMaps(template.Id, [definition.Id]);

        var response = StageTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientProcedureStageDefinition>
        {
            [definition.Id] = definition
        });

        response.Stages.Should().ContainSingle().Which.Name.Should().Be(ConsultationStage);
    }

    [Test]
    public void ToResponse_DefinitionMissingFromTheLookupAndTheMap_ShowsABlankActiveStage()
    {
        var template = new PatientProcedureStageTemplate { Id = Guid.NewGuid(), Name = TemplateName };
        template.Maps = StageTemplateMapper.ToMaps(template.Id, [ConsultationDefinitionId]);
        template.Maps[0].StageDefinition = null!;

        var response = StageTemplateMapper.ToResponse(template, 0, new Dictionary<Guid, PatientProcedureStageDefinition>());

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

        var maps = StageTemplateMapper.ToMaps(templateId, [ConsultationDefinitionId, ProcedureDefinitionId, ConsultationDefinitionId]);

        maps.Select(m => m.StageOrder).Should().Equal(1, 2, 3);
        maps.Select(m => m.StageDefinitionId).Should().Equal(ConsultationDefinitionId, ProcedureDefinitionId, ConsultationDefinitionId);
        maps.Should().OnlyContain(m => m.TemplateId == templateId);
    }
}
