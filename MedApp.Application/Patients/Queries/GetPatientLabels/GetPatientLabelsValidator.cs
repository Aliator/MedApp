using FluentValidation;

namespace MedApp.Application.Patients.Queries.GetPatientLabels;

public sealed class GetPatientLabelsValidator : AbstractValidator<GetPatientLabelsQuery>
{
    /// <summary>The most a lookup takes: enough for every pin, and no use as a way to list patients.</summary>
    public const int MaxIds = 20;

    public GetPatientLabelsValidator()
    {
        RuleFor(x => x.Ids)
            .Must(ids => ids.Count <= MaxIds)
            .WithMessage($"Ask for at most {MaxIds} patients at a time.");
    }
}
