using FluentValidation;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

public sealed class DecideNoteDeletionRequestValidator : AbstractValidator<DecideNoteDeletionRequestCommand>
{
    public DecideNoteDeletionRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("Request id is required.");

        // Only deleting needs these: keeping the note ignores them.
        When(x => x.DeleteNote, () =>
        {
            RuleFor(x => x.ExpectedVersion).NotNull().GreaterThan(0).WithMessage("The note's version is required.");
            // Left out, the requester's reason and detail are used; given, it must be one of the reasons.
            RuleFor(x => x.Reason).DeletionReason().When(x => x.Reason is not null);
            RuleFor(x => x.Detail).DeletionDetail();
            RuleFor(x => x.Detail)
                .Must(detail => !string.IsNullOrWhiteSpace(detail))
                .When(x => NoteDeletionReasons.IsOther(x.Reason))
                .WithMessage(NoteDeletionReasons.OtherNeedsDetailMessage);
        });
    }
}
