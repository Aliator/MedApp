using FluentAssertions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;

[TestFixture]
public sealed class DeletePathwayNoteValidatorTests
{
    private readonly DeletePathwayNoteValidator _validator = new();

    private static DeletePathwayNoteCommand Command(
        string reason = "WrongPatient", string? detail = null, int version = 1, Guid? pathwayId = null, Guid? noteId = null) =>
        new(pathwayId ?? Guid.NewGuid(), noteId ?? Guid.NewGuid(), reason, detail, version, Admin);

    [TestCase("WrongPatient")]
    [TestCase("writtenInError")]
    [TestCase("Duplicate")]
    public void Validate_AReasonWithoutDetail_IsValid(string reason) =>
        _validator.Validate(Command(reason)).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("Bored")]
    [TestCase("0")]
    public void Validate_NotAReason_IsInvalid(string reason) =>
        _validator.Validate(Command(reason)).Errors.Should().ContainSingle(e => e.PropertyName == "Reason");

    [TestCase(null)]
    [TestCase("  ")]
    public void Validate_OtherWithoutDetail_IsInvalid(string? detail) =>
        _validator.Validate(Command("Other", detail)).Errors
            .Should().ContainSingle(e => e.PropertyName == "Detail" && e.ErrorMessage == NoteDeletionReasons.OtherNeedsDetailMessage);

    [Test]
    public void Validate_OtherWithDetail_IsValid() =>
        _validator.Validate(Command("Other", DeletionDetail)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_DetailAtTheLimit_IsValid() =>
        _validator.Validate(Command(detail: new string('a', 500))).IsValid.Should().BeTrue();

    [Test]
    public void Validate_DetailTooLong_IsInvalid() =>
        _validator.Validate(Command(detail: new string('a', 501))).Errors.Should().ContainSingle(e => e.PropertyName == "Detail");

    [Test]
    public void Validate_NoVersion_IsInvalid() =>
        _validator.Validate(Command(version: 0)).Errors.Should().ContainSingle(e => e.PropertyName == "ExpectedVersion");

    [Test]
    public void Validate_NoIds_IsInvalid() =>
        _validator.Validate(Command(pathwayId: Guid.Empty, noteId: Guid.Empty)).Errors.Select(e => e.PropertyName)
            .Should().BeEquivalentTo("PathwayId", "NoteId");
}
