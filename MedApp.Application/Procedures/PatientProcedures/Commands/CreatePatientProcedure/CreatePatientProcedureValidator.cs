using FluentValidation;
using MedApp.Application.Procedures.Common;

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
            .MaximumLength(4000)
            .WithMessage("Notes must not exceed 4000 characters.");

        RuleFor(x => x.Priority)
            .Must(ProcedureEnums.IsPriority)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");

        RuleFor(x => x.DueDate)
            .Must(dueDate => dueDate >= DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The due date must not be in the past.");
    }
}
