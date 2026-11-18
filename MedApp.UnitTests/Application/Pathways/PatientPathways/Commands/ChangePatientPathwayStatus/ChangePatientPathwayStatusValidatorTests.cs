using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;

[TestFixture]
public sealed class ChangePatientPathwayStatusValidatorTests
{
    private readonly ChangePatientPathwayStatusValidator _validator = new();

    [TestCase("Blocked")]
    [TestCase("cancelled")]
    public void Validate_BlockOrCancelWithoutAReason_Fails(string status)
    {
        _validator.Validate(new ChangePatientPathwayStatusCommand(Guid.NewGuid(), status, " ", Staff)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_BlockWithAReason_Passes()
    {
        _validator.Validate(new ChangePatientPathwayStatusCommand(Guid.NewGuid(), "Blocked", Reason, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ArchiveWithoutAReason_Passes()
    {
        _validator.Validate(new ChangePatientPathwayStatusCommand(Guid.NewGuid(), "Archived", null, Staff)).IsValid.Should().BeTrue();
    }

    [TestCase("NotStarted")]
    [TestCase("")]
    public void Validate_UnknownStatus_Fails(string status)
    {
        _validator.Validate(new ChangePatientPathwayStatusCommand(Guid.NewGuid(), status, null, Staff)).IsValid.Should().BeFalse();
    }
}
