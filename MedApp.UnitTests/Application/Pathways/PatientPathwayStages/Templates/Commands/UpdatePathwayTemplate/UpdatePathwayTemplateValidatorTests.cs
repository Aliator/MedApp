using FluentAssertions;
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
    public void Validate_EmptySpecialty_IsInvalid() =>
        _validator.Validate(new UpdatePathwayTemplateCommand(Guid.NewGuid(), null, Guid.Empty, null)).IsValid.Should().BeFalse();
}
