using FluentValidation;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

public sealed class CreatePatientProcedureValidator : AbstractValidator<CreatePatientProcedureCommand>
{
    public CreatePatientProcedureValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient is required.");

        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Template is required.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .NotNull()
            .WithMessage("Notes must not be null.")
            .MaximumLength(PatientProcedureNote.MaxTextLength)
            .WithMessage($"Notes must not exceed {PatientProcedureNote.MaxTextLength} characters.");

        RuleFor(x => x.Priority)
            .Must(ProcedureEnums.IsPriority)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");

        RuleFor(x => x.DueDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Due date is required.")
            .Must(dueDate => dueDate >= DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The due date must not be in the past.");
    }
}
