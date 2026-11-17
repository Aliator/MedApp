using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

public sealed class DecideNoteDeletionRequestValidator : AbstractValidator<DecideNoteDeletionRequestCommand>
{
    public DecideNoteDeletionRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty().WithMessage("Request id is required.");
        // Left out, the requester's reason is used; given, it must say something.
        RuleFor(x => x.Reason!).DeletionReason().When(x => x.DeleteNote && x.Reason is not null);
    }
}
