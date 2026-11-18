using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;
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
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoStages_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePathwayTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, stages)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == $"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_NullStageList_FailsAsRequiredOnly()
    {
        var result = _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().Contain("Stage definition ids are required.")
            .And.NotContain($"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(" ", SpecialtyId, [ConsultationDefinitionId])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoSpecialty_IsInvalid() =>
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, Guid.Empty, [ConsultationDefinitionId])).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "Specialty is required.");

    [Test]
    public void Validate_WellFormedBranches_Passes()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId,
                [ConsultationDefinitionId, PathwayDefinitionId, PathwayDefinitionId, ConsultationDefinitionId],
                [new(ChoosesBranch: true), new(Branch: "Benign"), new(Branch: "Treatment"), TemplateStageOptions.Plain]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_OptionsForADifferentNumberOfStages_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId, [ConsultationDefinitionId], [TemplateStageOptions.Plain, TemplateStageOptions.Plain]))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == TemplateBranchRules.CountMismatchMessage);
    }

    [Test]
    public void Validate_BranchesBreakingTheRules_FailsWithTheRule()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, SpecialtyId,
                [ConsultationDefinitionId, PathwayDefinitionId], [TemplateStageOptions.Plain, new(IsOptional: true)]))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == TemplateBranchRules.LastStageMessage);
    }
}
