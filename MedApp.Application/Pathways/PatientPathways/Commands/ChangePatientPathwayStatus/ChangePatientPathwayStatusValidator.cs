using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;

public sealed class ChangePatientPathwayStatusValidator : AbstractValidator<ChangePatientPathwayStatusCommand>
{
    public ChangePatientPathwayStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Pathway id is required.");

        RuleFor(x => x.Status)
            .Must(PathwayEnums.IsStatus)
            .WithMessage($"Status must be one of: {PathwayEnums.StatusNames}.");

        RuleFor(x => x.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .When(x => PathwayEnums.TryParse<PatientPathwayStatus>(x.Status, out var status)
                       && PathwayWorkflow.RequiresReason(status))
            .WithMessage("A reason is required to put a pathway on hold or cancel it.");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason must not exceed 500 characters.");
    }
}
