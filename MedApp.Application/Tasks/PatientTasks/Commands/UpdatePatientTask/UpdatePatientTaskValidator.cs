using FluentValidation;
using MedApp.Application.Tasks.Repositories;
using MedApp.Domain.Tasks.PatientTasks;

namespace MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;

public sealed class UpdatePatientTaskValidator : AbstractValidator<UpdatePatientTaskCommand>
{
    public UpdatePatientTaskValidator(IPatientTaskStagesRepository taskStagesRepository)
    {
        RuleFor(x => x.PatientTaskId)
            .NotEmpty()
            .WithMessage("Patient task id is required.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .When(x => x.Title is not null)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(4000)
            .When(x => x.Notes is not null)
            .WithMessage("Notes must not exceed 4000 characters.");

        RuleFor(x => x.Priority)
            .Must(value => value is null || Enum.TryParse<PatientTaskPriority>(value, true, out _))
            .WithMessage("Priority is invalid.");

        RuleFor(x => x.Status)
            .Must(value => value is null || Enum.TryParse<PatientTaskStatus>(value, true, out _))
            .WithMessage("Status is invalid.");

        RuleForEach(x => x.StageDefinitionIdsInOrder!)
            .NotEmpty()
            .When(x => x.StageDefinitionIdsInOrder is not null)
            .WithMessage("Stage definition id is required.");
    }
}