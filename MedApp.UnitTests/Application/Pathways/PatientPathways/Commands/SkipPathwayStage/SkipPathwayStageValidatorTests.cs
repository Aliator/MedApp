using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;

[TestFixture]
public sealed class SkipPathwayStageValidatorTests
{
    private readonly SkipPathwayStageValidator _validator = new();

    private static SkipPathwayStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), "NotNeeded", null, Staff);

    [TestCase("NotNeeded")]
    [TestCase("patientdeclined")]
    public void Validate_ReasonStaffCanChoose_Passes(string reason)
    {
        _validator.Validate(Valid() with { Reason = reason }).IsValid.Should().BeTrue();
    }

    [TestCase("BranchNotChosen")]
    [TestCase("1")]
    [TestCase("Exploded")]
    [TestCase("")]
    public void Validate_ReasonNotForStaff_Fails(string reason)
    {
        var result = _validator.Validate(Valid() with { Reason = reason });

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == SkipPathwayStageValidator.ReasonMessage);
    }

    [TestCase(null)]
    [TestCase("  ")]
    public void Validate_OtherWithoutDetail_Fails(string? detail)
    {
        var result = _validator.Validate(Valid() with { Reason = "Other", Detail = detail });

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == SkipPathwayStageValidator.OtherNeedsDetailMessage);
    }

    [Test]
    public void Validate_OtherWithDetail_Passes()
    {
        _validator.Validate(Valid() with { Reason = "Other", Detail = SkipDetail }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_DetailTooLong_Fails()
    {
        _validator.Validate(Valid() with { Detail = new string('x', 501) }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoIds_Fails()
    {
        _validator.Validate(Valid() with { PathwayId = Guid.Empty, StageId = Guid.Empty }).Errors.Should().HaveCount(2);
    }
}
