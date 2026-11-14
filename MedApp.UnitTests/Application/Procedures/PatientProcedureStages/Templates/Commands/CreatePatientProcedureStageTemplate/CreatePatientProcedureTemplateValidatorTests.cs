using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

[TestFixture]
public sealed class CreatePatientProcedureTemplateValidatorTests
{
    private readonly CreatePatientProcedureTemplateValidator _validator = new();

    [Test]
    public void Validate_RepeatedStage_Passes()
    {
        _validator.Validate(new CreatePatientProcedureTemplateCommand(TemplateName, [ConsultationDefinitionId, ProcedureDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoStages_Fails()
    {
        _validator.Validate(new CreatePatientProcedureTemplateCommand(TemplateName, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePatientProcedureTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new CreatePatientProcedureTemplateCommand(TemplateName, stages)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new CreatePatientProcedureTemplateCommand(" ", [ConsultationDefinitionId])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new CreatePatientProcedureTemplateCommand(TemplateName, [Guid.Empty])).IsValid.Should().BeFalse();
    }
}
