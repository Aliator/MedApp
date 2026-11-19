using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;

[TestFixture]
public sealed class AddPathwayBranchValidatorTests
{
    private readonly AddPathwayBranchValidator _validator = new();

    private static AddPathwayBranchCommand Command(string? reason = "Synthetic: an insufficient sample.") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason, Staff);

    [Test]
    public void Validate_AReason_Passes() => _validator.Validate(Command()).IsValid.Should().BeTrue();

    [TestCase(null)]
    [TestCase("  ")]
    public void Validate_NoReason_SaysWhyItsNeeded(string? reason) =>
        _validator.Validate(Command(reason)).Errors.Should().ContainSingle(e => e.ErrorMessage == PathwayBranches.ReasonRequiredMessage);

    [Test]
    public void Validate_AReasonTooLong_SaysTheLimit() =>
        _validator.Validate(Command(new string('x', 501))).Errors.Should().ContainSingle(e => e.ErrorMessage == PathwayBranches.ReasonTooLongMessage);

    [Test]
    public void Validate_NoBranchOrStage_SaysChoose()
    {
        var errors = _validator.Validate(new AddPathwayBranchCommand(Guid.NewGuid(), Guid.Empty, Guid.Empty, "Synthetic.", Staff)).Errors;

        errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo(PathwayBranches.NotTheTemplatesMessage, PathwayBranches.ChooseAfterStageMessage);
    }
}
