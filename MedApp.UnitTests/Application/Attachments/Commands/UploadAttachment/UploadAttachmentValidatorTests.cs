using FluentAssertions;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Commands.UploadAttachment;

[TestFixture]
public sealed class UploadAttachmentValidatorTests
{
    private readonly UploadAttachmentValidator _validator = new();

    private static UploadAttachmentCommand Command(Guid? patientId, Guid? procedureId, Guid? stageId, string? fileName = FileName) =>
        new(patientId, procedureId, stageId, fileName, 10, Stream(Pdf), Staff, false);

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public void Validate_ExactlyOneTarget_IsValid(bool patient, bool procedure, bool stage)
    {
        var result = _validator.Validate(Command(
            patient ? Guid.NewGuid() : null, procedure ? Guid.NewGuid() : null, stage ? Guid.NewGuid() : null));

        result.IsValid.Should().BeTrue();
    }

    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public void Validate_NoneOrSeveralTargets_IsInvalid(bool patient, bool procedure, bool stage)
    {
        var result = _validator.Validate(Command(
            patient ? Guid.NewGuid() : null, procedure ? Guid.NewGuid() : null, stage ? Guid.NewGuid() : null));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Target"
                                                  && e.ErrorMessage == "Give exactly one of a patient, a procedure or a stage.");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("‮/")]
    public void Validate_NoUsableName_IsInvalid(string? name)
    {
        var result = _validator.Validate(Command(Guid.NewGuid(), null, null, name));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "FileName" && e.ErrorMessage == "Give the file's name.");
    }

    [Test]
    public void Validate_NameTooLong_IsInvalid()
    {
        var result = _validator.Validate(Command(Guid.NewGuid(), null, null, new string('a', UploadAttachmentValidator.MaxRawFileNameLength) + ".pdf"));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "FileName" && e.ErrorMessage.StartsWith("The file's name must be at most"));
    }
}
