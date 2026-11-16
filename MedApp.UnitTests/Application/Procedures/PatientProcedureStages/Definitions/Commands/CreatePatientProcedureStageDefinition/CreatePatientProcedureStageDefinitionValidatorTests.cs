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

    [TestCase("None")]
    [TestCase("Suggested")]
    [TestCase("required")]
    public void Validate_KnownVisitRequirement_Passes(string requirement)
    {
        _validator.Validate(Valid() with { VisitRequirement = requirement }).IsValid.Should().BeTrue();
    }

    [TestCase("Sometimes")]
    [TestCase("2")]
    [TestCase("")]
    public void Validate_UnknownVisitRequirement_FailsListingTheNames(string requirement)
    {
        var result = _validator.Validate(Valid() with { VisitRequirement = requirement });

        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "VisitRequirement"
            && e.ErrorMessage == "Visit requirement must be one of: None, Suggested, Required.");
    }
}
