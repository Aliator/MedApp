using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

[TestFixture]
public sealed class UnlockPathwayStageValidatorTests
{
    private readonly UnlockPathwayStageValidator _validator = new();

    private static UnlockPathwayStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Admin);

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
    public void Validate_NoStageId_Fails()
    {
        _validator.Validate(Valid() with { StageId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
