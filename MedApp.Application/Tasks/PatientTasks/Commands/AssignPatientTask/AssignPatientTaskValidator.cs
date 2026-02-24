using FluentValidation;

namespace MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;

public sealed class AssignPatientTaskValidator : AbstractValidator<AssignPatientTaskCommand>
{
    public AssignPatientTaskValidator()
    {
        RuleFor(x => x.PatientTaskId)
            .NotEmpty()
            .WithMessage("Patient task id is required.");

        RuleFor(x => x.AssignedByUserId)
            .NotEmpty()
            .WithMessage("Assigned by user id is required.");

        RuleForEach(x => x.AssignedUserIds)
            .NotEmpty()
            .WithMessage("Assigned user id is required.");
    }
}