using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

[TestFixture]
public sealed class AssignPatientProcedureValidatorTests
{
    private readonly AssignPatientProcedureValidator _validator = new();

    private static AssignPatientProcedureCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Admin);

    [Test]
    public void Validate_BothIds_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoProcedure_Fails()
    {
        _validator.Validate(Valid() with { ProcedureId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoUserId_Fails()
    {
        _validator.Validate(Valid() with { UserId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
