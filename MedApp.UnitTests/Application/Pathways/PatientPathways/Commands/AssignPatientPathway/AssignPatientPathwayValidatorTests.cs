using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

[TestFixture]
public sealed class AssignPatientPathwayValidatorTests
{
    private readonly AssignPatientPathwayValidator _validator = new();

    private static AssignPatientPathwayCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Admin);

    [Test]
    public void Validate_BothIds_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoPathway_Fails()
    {
        _validator.Validate(Valid() with { PathwayId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoUserId_Fails()
    {
        _validator.Validate(Valid() with { UserId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
