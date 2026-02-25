using FluentValidation;
using MedApp.Domain.Tasks.PatientTasks;

namespace MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;

public sealed class CreatePatientTaskValidator : AbstractValidator<CreatePatientTaskCommand>
{
    public CreatePatientTaskValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("Patient id is required.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .NotEmpty()
            .WithMessage("Notes are required.")
            .MaximumLength(4000)
            .WithMessage("Notes must not exceed 4000 characters.");

        RuleFor(x => x.Priority)
            .Must(value => Enum.TryParse<PatientTaskPriority>(value, true, out _))
            .WithMessage("Priority is invalid.");
        
        RuleFor(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("At least one stage definition is required.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Stage definition ids must be unique.");

        RuleForEach(x => x.StageDefinitionIdsInOrder)
            .NotEmpty()
            .WithMessage("Stage definition id is required.");
    }
}
