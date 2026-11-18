using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;

public sealed class UnassignPatientPathwayValidator : AbstractValidator<UnassignPatientPathwayCommand>
{
    public UnassignPatientPathwayValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User id is required.");
    }
}
