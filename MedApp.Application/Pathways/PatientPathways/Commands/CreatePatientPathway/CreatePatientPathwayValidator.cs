using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;

public sealed class CreatePatientPathwayValidator : AbstractValidator<CreatePatientPathwayCommand>
{
    public CreatePatientPathwayValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient is required.");

        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Template is required.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Notes)
            .NotNull()
            .WithMessage("Notes must not be null.")
            .MaximumLength(PatientPathwayNote.MaxTextLength)
            .WithMessage($"Notes must not exceed {PatientPathwayNote.MaxTextLength} characters.");

        RuleFor(x => x.Priority)
            .Must(PathwayEnums.IsPriority)
            .WithMessage($"Priority must be one of: {PathwayEnums.PriorityNames}.");

        RuleFor(x => x.DueDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Target date is required.")
            .Must(dueDate => dueDate >= DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage("The target date must not be in the past.");
    }
}
