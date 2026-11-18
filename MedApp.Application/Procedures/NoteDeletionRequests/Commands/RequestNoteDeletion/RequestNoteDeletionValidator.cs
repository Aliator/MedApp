using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

public sealed class RequestNoteDeletionValidator : AbstractValidator<RequestNoteDeletionCommand>
{
    public RequestNoteDeletionValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => (string?)x.Reason).DeletionReason().OverridePropertyName(nameof(RequestNoteDeletionCommand.Reason));
        RuleFor(x => x.Detail).DeletionDetail();
        RuleFor(x => x.Detail)
            .Must(detail => !string.IsNullOrWhiteSpace(detail))
            .When(x => NoteDeletionReasons.IsOther(x.Reason))
            .WithMessage(NoteDeletionReasons.OtherNeedsDetailMessage);
    }
}
