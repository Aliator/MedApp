using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;

[TestFixture]
public sealed class UpdateProcedureStageNotesValidatorTests
{
    private readonly UpdateProcedureStageNotesValidator _validator = new();

    [Test]
    public void Validate_EmptyNotes_Passes()
    {
        _validator.Validate(new UpdateProcedureStageNotesCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty, Staff))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        _validator.Validate(new UpdateProcedureStageNotesCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', MaxNotesLength + 1), Staff))
            .IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoStage_Fails()
    {
        _validator.Validate(new UpdateProcedureStageNotesCommand(Guid.NewGuid(), Guid.Empty, "x", Staff))
            .IsValid.Should().BeFalse();
    }
}
