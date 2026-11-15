using FluentValidation;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

public sealed class UpdatePatientProcedureValidator : AbstractValidator<UpdatePatientProcedureCommand>
{
    public UpdatePatientProcedureValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Procedure id is required.");

        RuleFor(x => x.Title)
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .When(x => x.Title is not null)
            .WithMessage("Title must not be blank.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(4000)
            .WithMessage("Notes must not exceed 4000 characters.");

        RuleFor(x => x.Priority)
            .Must(ProcedureEnums.IsPriority)
            .When(x => x.Priority is not null)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");
    }
}
