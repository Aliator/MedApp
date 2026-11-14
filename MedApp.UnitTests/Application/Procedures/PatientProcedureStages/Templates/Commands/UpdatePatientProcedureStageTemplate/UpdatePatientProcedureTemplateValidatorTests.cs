using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;

[TestFixture]
public sealed class UpdatePatientProcedureTemplateValidatorTests
{
    private readonly UpdatePatientProcedureTemplateValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), null, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_RepeatedStages_Passes()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), TemplateName, [ConsultationDefinitionId, ConsultationDefinitionId]))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoTemplate_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.Empty, null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), " ", null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageList_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), null, [])).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TooManyStages_Fails()
    {
        var stages = Enumerable.Repeat(ConsultationDefinitionId, CreatePatientProcedureTemplateValidator.MaxStages + 1).ToList();

        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), null, stages)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_EmptyStageId_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureTemplateCommand(Guid.NewGuid(), null, [Guid.Empty])).IsValid.Should().BeFalse();
    }
}
