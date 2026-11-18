using FluentValidation;
using MedApp.Application.Attachments.Common;

namespace MedApp.Application.Attachments.Commands.UploadAttachment;

public sealed class UploadAttachmentValidator : AbstractValidator<UploadAttachmentCommand>
{
    /// <summary>The longest name accepted before cleaning (the kept name is shortened to <see cref="AttachmentLimits.MaxFileNameLength"/>).</summary>
    public const int MaxRawFileNameLength = 1024;

    public UploadAttachmentValidator()
    {
        RuleFor(x => x)
            .Must(x => new[] { x.PatientId, x.PathwayId, x.StageId, x.ReferralId }.Count(id => id is not null) == 1)
            .OverridePropertyName("Target")
            .WithMessage("Give exactly one of a patient, a pathway, a stage or a referral.");

        RuleFor(x => x.FileName)
            .Must(name => AttachmentFileName.Clean(name) is not null)
            .WithMessage("Give the file's name.")
            .MaximumLength(MaxRawFileNameLength)
            .WithMessage($"The file's name must be at most {MaxRawFileNameLength} characters.");
    }
}
