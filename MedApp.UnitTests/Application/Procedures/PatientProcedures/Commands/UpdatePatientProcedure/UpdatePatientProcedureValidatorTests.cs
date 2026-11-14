using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

[TestFixture]
public sealed class UpdatePatientProcedureValidatorTests
{
    private readonly UpdatePatientProcedureValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), null, null, null, null, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlankTitle_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), "  ", null, null, null, Staff)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_UnknownPriority_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), null, null, null, "Soon", Staff)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        var command = new UpdatePatientProcedureCommand(Guid.NewGuid(), null, new string('a', MaxNotesLength + 1), null, null, Staff);

        _validator.Validate(command).IsValid.Should().BeFalse();
    }
}
