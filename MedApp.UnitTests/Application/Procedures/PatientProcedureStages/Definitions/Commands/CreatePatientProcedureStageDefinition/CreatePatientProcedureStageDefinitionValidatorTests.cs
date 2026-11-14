using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;

[TestFixture]
public sealed class CreatePatientProcedureStageDefinitionValidatorTests
{
    private readonly CreatePatientProcedureStageDefinitionValidator _validator = new();

    private static CreatePatientProcedureStageDefinitionCommand Valid() => new(ConsultationStage, "Description", "Instructions");

    [Test]
    public void Validate_EveryFieldSet_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(Valid() with { Name = "  " }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        _validator.Validate(Valid() with { Name = new string('a', 101) }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoDescription_Fails()
    {
        _validator.Validate(Valid() with { Description = "" }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_InstructionsTooLong_Fails()
    {
        _validator.Validate(Valid() with { Instructions = new string('a', 2001) }).IsValid.Should().BeFalse();
    }
}
