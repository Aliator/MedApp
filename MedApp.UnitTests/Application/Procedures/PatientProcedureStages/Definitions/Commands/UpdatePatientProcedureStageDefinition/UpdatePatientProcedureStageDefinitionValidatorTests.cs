using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;

[TestFixture]
public sealed class UpdatePatientProcedureStageDefinitionValidatorTests
{
    private readonly UpdatePatientProcedureStageDefinitionValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), null, null, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoDefinition_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.Empty, null, null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlankName_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), " ", null, null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlankDescription_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), null, "", null)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_InstructionsTooLong_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), null, null, new string('a', 2001)))
            .IsValid.Should().BeFalse();
    }

    [TestCase("Suggested")]
    [TestCase("none")]
    public void Validate_KnownVisitRequirement_Passes(string requirement)
    {
        _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), null, null, null, VisitRequirement: requirement))
            .IsValid.Should().BeTrue();
    }

    [TestCase("Always")]
    [TestCase("1")]
    public void Validate_UnknownVisitRequirement_FailsListingTheNames(string requirement)
    {
        var result = _validator.Validate(new UpdatePatientProcedureStageDefinitionCommand(Guid.NewGuid(), null, null, null, VisitRequirement: requirement));

        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "VisitRequirement"
            && e.ErrorMessage == "Visit requirement must be one of: None, Suggested, Required.");
    }
}
