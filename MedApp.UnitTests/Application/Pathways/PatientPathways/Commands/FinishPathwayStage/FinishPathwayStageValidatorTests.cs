using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;

[TestFixture]
public sealed class FinishPathwayStageValidatorTests
{
    private readonly FinishPathwayStageValidator _validator = new();

    private static FinishPathwayStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Staff);

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

    [Test]
    public void Validate_ReasonOf500Characters_Passes()
    {
        _validator.Validate(Valid() with { WithoutVisit = true, Reason = new string('r', 500) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ReasonOver500Characters_FailsOnTheReason()
    {
        var result = _validator.Validate(Valid() with { WithoutVisit = true, Reason = new string('r', 501) });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Reason" && e.ErrorMessage == "Reason must not exceed 500 characters.");
    }

    [Test]
    public void Validate_WithoutVisitAndNoReason_PassesAsTheHandlerDecidesWhetherOneIsNeeded()
    {
        _validator.Validate(Valid() with { WithoutVisit = true, Reason = null }).IsValid.Should().BeTrue();
    }
}
