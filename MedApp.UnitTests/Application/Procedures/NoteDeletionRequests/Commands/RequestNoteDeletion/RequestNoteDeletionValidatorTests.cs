using FluentAssertions;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

[TestFixture]
public sealed class RequestNoteDeletionValidatorTests
{
    private readonly RequestNoteDeletionValidator _validator = new();

    private static RequestNoteDeletionCommand Command(string reason = DeletionReason, Guid? procedureId = null, Guid? noteId = null) =>
        new(procedureId ?? Guid.NewGuid(), noteId ?? Guid.NewGuid(), reason, Staff);

    [Test]
    public void Validate_ValidCommand_IsValid() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase(" \t ")]
    public void Validate_NoReason_IsInvalid(string reason) =>
        _validator.Validate(Command(reason)).Errors
            .Should().ContainSingle(e => e.PropertyName == "Reason" && e.ErrorMessage == "Say why the note should be deleted.");

    [Test]
    public void Validate_ReasonTooLong_IsInvalid() =>
        _validator.Validate(Command(new string('a', 501))).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "A reason must not exceed 500 characters.");

    [Test]
    public void Validate_NoIds_IsInvalid() =>
        _validator.Validate(Command(procedureId: Guid.Empty, noteId: Guid.Empty)).Errors.Select(e => e.PropertyName)
            .Should().BeEquivalentTo("ProcedureId", "NoteId");
}
