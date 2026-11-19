using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;

[TestFixture]
public sealed class PickUpPathwayStageValidatorTests
{
    private readonly PickUpPathwayStageValidator _validator = new();

    [Test]
    public void Validate_BothIds_Passes() =>
        _validator.Validate(new PickUpPathwayStageCommand(Guid.NewGuid(), Guid.NewGuid(), Staff)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_EmptyIds_Fails() =>
        _validator.Validate(new PickUpPathwayStageCommand(Guid.Empty, Guid.Empty, Staff)).Errors.Select(e => e.ErrorMessage)
            .Should().BeEquivalentTo("Pathway id is required.", "Stage id is required.");
}
