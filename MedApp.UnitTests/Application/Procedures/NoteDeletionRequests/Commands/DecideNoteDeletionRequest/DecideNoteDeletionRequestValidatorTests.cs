using FluentAssertions;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

[TestFixture]
public sealed class DecideNoteDeletionRequestValidatorTests
{
    private readonly DecideNoteDeletionRequestValidator _validator = new();

    [TestCase(true)]
    [TestCase(false)]
    public void Validate_NoReason_IsValid(bool deleteNote) =>
        // Deleting without one uses the requester's reason.
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), deleteNote, null, Admin)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_DeletingWithAReason_IsValid() =>
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), true, DeletionReason, Admin)).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_DeletingWithABlankReason_IsInvalid(string reason) =>
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), true, reason, Admin)).Errors
            .Should().ContainSingle(e => e.PropertyName == "Reason");

    [Test]
    public void Validate_DeletingWithAReasonTooLong_IsInvalid() =>
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), true, new string('a', 501), Admin)).Errors
            .Should().ContainSingle(e => e.PropertyName == "Reason");

    [Test]
    public void Validate_KeepingWithABlankReason_IsValid() =>
        // Keeping the note ignores the reason.
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), false, " ", Admin)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoRequestId_IsInvalid() =>
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.Empty, false, null, Admin)).Errors
            .Should().ContainSingle(e => e.PropertyName == "RequestId");
}
