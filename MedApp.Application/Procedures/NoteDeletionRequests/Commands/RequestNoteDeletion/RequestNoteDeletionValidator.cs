using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;

public sealed class RequestNoteDeletionValidator : AbstractValidator<RequestNoteDeletionCommand>
{
    public RequestNoteDeletionValidator()
    {
        RuleFor(x => x.ProcedureId).NotEmpty().WithMessage("Procedure id is required.");
        RuleFor(x => x.NoteId).NotEmpty().WithMessage("Note id is required.");
        RuleFor(x => x.Reason).DeletionReason();
    }
}
