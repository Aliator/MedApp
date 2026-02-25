using FluentValidation;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskValidator : AbstractValidator<AssignPatientTaskCommand>
{
    public AssignPatientTaskValidator()
    {
        RuleFor(x => x.PatientTaskId)
            .NotEmpty()
            .WithMessage("Patient task id is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User id is required.");
    }
}