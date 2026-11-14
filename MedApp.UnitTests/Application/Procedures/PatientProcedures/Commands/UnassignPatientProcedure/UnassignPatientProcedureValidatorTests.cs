using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;

[TestFixture]
public sealed class UnassignPatientProcedureValidatorTests
{
    private readonly UnassignPatientProcedureValidator _validator = new();

    private static UnassignPatientProcedureCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Admin);

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
