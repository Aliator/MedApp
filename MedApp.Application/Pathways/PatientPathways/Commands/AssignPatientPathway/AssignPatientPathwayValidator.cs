using FluentValidation;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

public sealed class AssignPatientPathwayValidator : AbstractValidator<AssignPatientPathwayCommand>
{
    public AssignPatientPathwayValidator()
    {
        RuleFor(x => x.PathwayId).NotEmpty().WithMessage("Pathway id is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User id is required.");
    }
}
