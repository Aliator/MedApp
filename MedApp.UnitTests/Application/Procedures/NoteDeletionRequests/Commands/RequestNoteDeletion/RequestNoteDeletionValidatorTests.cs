using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

[TestFixture]
public sealed class RequestNoteDeletionValidatorTests
{
    private readonly RequestNoteDeletionValidator _validator = new();

    private static RequestNoteDeletionCommand Command(string reason = "WrongPatient", string? detail = DeletionDetail, Guid? procedureId = null, Guid? noteId = null) =>
        new(procedureId ?? Guid.NewGuid(), noteId ?? Guid.NewGuid(), reason, detail, Staff);

    [Test]
    public void Validate_ValidCommand_IsValid() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoDetail_IsValid() =>
        _validator.Validate(Command(detail: null)).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("Because")]
    public void Validate_NotAReason_IsInvalid(string reason) =>
        _validator.Validate(Command(reason)).Errors.Should().ContainSingle(e => e.PropertyName == "Reason");

    [Test]
    public void Validate_OtherWithoutDetail_IsInvalid() =>
        _validator.Validate(Command("Other", " ")).Errors
            .Should().ContainSingle(e => e.ErrorMessage == NoteDeletionReasons.OtherNeedsDetailMessage);

    [Test]
    public void Validate_DetailTooLong_IsInvalid() =>
        _validator.Validate(Command(detail: new string('a', 501))).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "The detail must not exceed 500 characters.");

    [Test]
    public void Validate_NoIds_IsInvalid() =>
        _validator.Validate(Command(procedureId: Guid.Empty, noteId: Guid.Empty)).Errors.Select(e => e.PropertyName)
            .Should().BeEquivalentTo("ProcedureId", "NoteId");
}
