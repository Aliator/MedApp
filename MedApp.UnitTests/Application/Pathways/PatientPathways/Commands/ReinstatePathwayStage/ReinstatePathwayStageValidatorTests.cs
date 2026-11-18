using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;

[TestFixture]
public sealed class ReinstatePathwayStageValidatorTests
{
    private readonly ReinstatePathwayStageValidator _validator = new();

    private static ReinstatePathwayStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), SkipDetail, Admin);

    [Test]
    public void Validate_WithAReason_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoReason_Fails(string reason)
    {
        _validator.Validate(Valid() with { Reason = reason }).Errors
            .Should().ContainSingle(e => e.ErrorMessage == ReinstatePathwayStageValidator.ReasonRequiredMessage);
    }

    [Test]
    public void Validate_ReasonTooLong_Fails()
    {
        _validator.Validate(Valid() with { Reason = new string('x', 501) }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoIds_Fails()
    {
        _validator.Validate(Valid() with { PathwayId = Guid.Empty, StageId = Guid.Empty }).Errors.Should().HaveCount(2);
    }
}
