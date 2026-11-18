using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

[TestFixture]
public sealed class CreatePathwayTemplateValidatorTests
{
    private readonly CreatePathwayTemplateValidator _validator = new();

    [Test]
    public void Validate_RepeatedStage_Passes()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoStages_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePathwayTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, stages)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == $"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_NullStageList_FailsAsRequiredOnly()
    {
        var result = _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().Contain("Stage definition ids are required.")
            .And.NotContain($"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(" ", [ConsultationDefinitionId])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, [Guid.Empty])).IsValid.Should().BeFalse();
    }
}
