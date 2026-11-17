using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed class UpdatePatientProcedureValidator : AbstractValidator<UpdatePatientProcedureCommand>
{
    /// <summary>The 400 for an edit that still sends the old single notes field (a page loaded before the change).</summary>
    public const string NotesMovedMessage =
        "Notes are now added on the procedure's page, one at a time, and can't be changed here. Reload the page, then add your note there.";

    public UpdatePatientProcedureValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Procedure id is required.");

        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .When(x => x.Title is not null)
            .WithMessage("Title must not be blank.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Priority)
            .Must(ProcedureEnums.IsPriority)
            .When(x => x.Priority is not null)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");

        RuleFor(x => x.Notes)
            .Null()
            .WithMessage(NotesMovedMessage);
    }
}
