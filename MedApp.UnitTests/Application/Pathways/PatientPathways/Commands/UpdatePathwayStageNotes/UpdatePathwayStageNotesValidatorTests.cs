using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

[TestFixture]
public sealed class UpdatePathwayStageNotesValidatorTests
{
    private readonly UpdatePathwayStageNotesValidator _validator = new();

    [Test]
    public void Validate_EmptyNotes_Passes()
    {
        _validator.Validate(new UpdatePathwayStageNotesCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty, Staff))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        _validator.Validate(new UpdatePathwayStageNotesCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', MaxNotesLength + 1), Staff))
            .IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoStage_Fails()
    {
        _validator.Validate(new UpdatePathwayStageNotesCommand(Guid.NewGuid(), Guid.Empty, "x", Staff))
            .IsValid.Should().BeFalse();
    }
}
