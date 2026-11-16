using FluentValidation;
using MedApp.Application.Patients.Queries.GetPatientLabels;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;

public sealed class GetPatientProcedureLabelsValidator : AbstractValidator<GetPatientProcedureLabelsQuery>
{
    public GetPatientProcedureLabelsValidator()
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids.Count <= GetPatientLabelsValidator.MaxIds)
            .WithMessage($"Ask for at most {GetPatientLabelsValidator.MaxIds} procedures at a time.");
    }
}
