using FluentAssertions;
using MedApp.Application.Pathways.PatientPathwayStages.Templates;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

[TestFixture]
public sealed class UpdatePathwayTemplateValidatorTests
{
    private readonly UpdatePathwayTemplateValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_RepeatedStages_Passes()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), TemplateName, null, [ConsultationDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoTemplate_Fails()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.Empty, null, null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), " ", null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageList_Fails()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePathwayTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, stages)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyTeam_IsInvalid() =>
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, Guid.Empty, null)).IsValid.Should().BeFalse();

    [Test]
    public void Validate_ClockStopStage_NeedsTheStageListAndIsOneOfItsStages()
    {
        var validator = new UpdatePathwayTemplateValidator();
        var id = Guid.NewGuid();

        validator.Validate(new UpdatePathwayTemplateCommand(id, null, null, [Guid.NewGuid(), Guid.NewGuid()], 2)).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePathwayTemplateCommand(id, null, null, [Guid.NewGuid()], null)).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePathwayTemplateCommand(id, "Renamed", null, null, 1)).Errors.Single().ErrorMessage
            .Should().Be("Send the stage list with the stage that stops the waiting-time clock.");
        validator.Validate(new UpdatePathwayTemplateCommand(id, null, null, [Guid.NewGuid()], 2)).Errors.Single().ErrorMessage
            .Should().Be(MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate.CreatePathwayTemplateValidator.ClockStopStageMessage);
    }

    [Test]
    public void Validate_OptionsWithoutAStageList_Fails()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, null, StageOptions: [TemplateStageOptions.Plain]))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == TemplateShape.CountMismatchMessage);
    }

    [Test]
    public void Validate_StagesWithoutOptions_PassesAsBefore()
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, [ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [TestCase(null, true)]
    [TestCase("Standard", true)]
    [TestCase("oncology", true)]
    [TestCase("MentalHealth", true)]
    [TestCase("1", false)]
    [TestCase("Dental", false)]
    public void Validate_RetentionClass_IsOneByName(string? value, bool valid)
    {
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, null, null, RetentionClass: value)).IsValid.Should().Be(valid);
    }
}
