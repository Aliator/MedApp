using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

[TestFixture]
public sealed class DecideNoteDeletionRequestValidatorTests
{
    private readonly DecideNoteDeletionRequestValidator _validator = new();

    private static DecideNoteDeletionRequestCommand Deleting(int? version = 1, string? reason = null, string? detail = null) =>
        new(Guid.NewGuid(), true, version, reason, detail, Admin);

    [Test]
    public void Validate_DeletingWithTheRequestersReason_IsValid() =>
        _validator.Validate(Deleting()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_DeletingWithAReasonAndDetail_IsValid() =>
        _validator.Validate(Deleting(reason: "Duplicate", detail: DeletionDetail)).IsValid.Should().BeTrue();

    [TestCase(null)]
    [TestCase(0)]
    public void Validate_DeletingWithoutTheVersion_IsInvalid(int? version) =>
        _validator.Validate(Deleting(version)).Errors.Should().ContainSingle(e => e.PropertyName == "ExpectedVersion");

    [Test]
    public void Validate_DeletingWithNotAReason_IsInvalid() =>
        _validator.Validate(Deleting(reason: "Whim")).Errors.Should().ContainSingle(e => e.PropertyName == "Reason");

    [Test]
    public void Validate_DeletingAsOtherWithoutDetail_IsInvalid() =>
        _validator.Validate(Deleting(reason: "Other")).Errors
            .Should().ContainSingle(e => e.PropertyName == "Detail" && e.ErrorMessage == NoteDeletionReasons.OtherNeedsDetailMessage);

    [Test]
    public void Validate_DeletingWithADetailTooLong_IsInvalid() =>
        _validator.Validate(Deleting(detail: new string('a', 501))).Errors.Should().ContainSingle(e => e.PropertyName == "Detail");

    [Test]
    public void Validate_KeepingWithNothingElse_IsValid() =>
        // Keeping the note ignores the version, reason and detail.
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.NewGuid(), false, null, "Whim", new string('a', 600), Admin))
            .IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoRequestId_IsInvalid() =>
        _validator.Validate(new DecideNoteDeletionRequestCommand(Guid.Empty, false, null, null, null, Admin)).Errors
            .Should().ContainSingle(e => e.PropertyName == "RequestId");
}
