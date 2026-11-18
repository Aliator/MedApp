using FluentValidation;
using MedApp.Application.Patients.Queries.GetPatientLabels;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;

public sealed class GetPatientPathwayLabelsValidator : AbstractValidator<GetPatientPathwayLabelsQuery>
{
    public GetPatientPathwayLabelsValidator()
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids.Count <= GetPatientLabelsValidator.MaxIds)
            .WithMessage($"Ask for at most {GetPatientLabelsValidator.MaxIds} pathways at a time.");
    }
}
