using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;

[TestFixture]
public sealed class ChangePatientProcedureStatusValidatorTests
{
    private readonly ChangePatientProcedureStatusValidator _validator = new();

    [TestCase("Blocked")]
    [TestCase("cancelled")]
    public void Validate_BlockOrCancelWithoutAReason_Fails(string status)
    {
        _validator.Validate(new ChangePatientProcedureStatusCommand(Guid.NewGuid(), status, " ", Staff)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlockWithAReason_Passes()
    {
        _validator.Validate(new ChangePatientProcedureStatusCommand(Guid.NewGuid(), "Blocked", Reason, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ArchiveWithoutAReason_Passes()
    {
        _validator.Validate(new ChangePatientProcedureStatusCommand(Guid.NewGuid(), "Archived", null, Staff)).IsValid.Should().BeTrue();
    }

    [TestCase("NotStarted")]
    [TestCase("")]
    public void Validate_UnknownStatus_Fails(string status)
    {
        _validator.Validate(new ChangePatientProcedureStatusCommand(Guid.NewGuid(), status, null, Staff)).IsValid.Should().BeFalse();
    }
}
