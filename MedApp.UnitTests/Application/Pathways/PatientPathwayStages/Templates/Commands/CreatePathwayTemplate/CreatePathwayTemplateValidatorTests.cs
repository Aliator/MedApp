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
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [TestCase(null, true)]
    [TestCase(1, true)]
    [TestCase(3, true)]
    [TestCase(0, false)]
    [TestCase(4, false)]
    public void Validate_ClockStopStage_IsOneOfItsStages(int? order, bool valid)
    {
        var result = _validator.Validate(new CreatePathwayTemplateCommand(
            TemplateName, TeamId, [ConsultationDefinitionId, PathwayDefinitionId, ConsultationDefinitionId], order));

        result.IsValid.Should().Be(valid);
        if (!valid)
            result.Errors.Single().ErrorMessage.Should().Be(CreatePathwayTemplateValidator.ClockStopStageMessage);
    }

    [Test]
    public void Validate_NoStages_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePathwayTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, stages)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == $"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_NullStageList_FailsAsRequiredOnly()
    {
        var result = _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().Contain("Stage definition ids are required.")
            .And.NotContain($"A template can have at most {CreatePathwayTemplateValidator.MaxStages} stages.");
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(" ", TeamId, [ConsultationDefinitionId])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoTeam_IsInvalid() =>
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, Guid.Empty, [ConsultationDefinitionId])).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "Team is required.");

    [Test]
    public void Validate_WellFormedBranches_Passes()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId,
                [ConsultationDefinitionId, PathwayDefinitionId, PathwayDefinitionId, ConsultationDefinitionId],
                StageOptions: [new(ChoosesBranch: true), new(Branch: "Benign"), new(Branch: "Treatment"), TemplateStageOptions.Plain]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_OptionsForADifferentNumberOfStages_Fails()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, [ConsultationDefinitionId], StageOptions: [TemplateStageOptions.Plain, TemplateStageOptions.Plain]))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == TemplateBranchRules.CountMismatchMessage);
    }

    [Test]
    public void Validate_BranchesBreakingTheRules_FailsWithTheRule()
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId,
                [ConsultationDefinitionId, PathwayDefinitionId], StageOptions: [TemplateStageOptions.Plain, new(IsOptional: true)]))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == TemplateBranchRules.LastStageMessage);
    }

    [TestCase(null, true)]
    [TestCase("Standard", true)]
    [TestCase("oncology", true)]
    [TestCase("MentalHealth", true)]
    [TestCase("1", false)]
    [TestCase("Dental", false)]
    public void Validate_RetentionClass_IsOneByName(string? value, bool valid)
    {
        _validator.Validate(new CreatePathwayTemplateCommand(TemplateName, TeamId, [ConsultationDefinitionId], RetentionClass: value)).IsValid.Should().Be(valid);
    }
}
