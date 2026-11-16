using FluentValidation;
using MedApp.Application.Fhir.Common;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Fhir.Queries.SearchFhirPatients;

public sealed class SearchFhirPatientsValidator : AbstractValidator<SearchFhirPatientsQuery>
{
    public const string IdentifierRequired =
        $"Search patients by identifier: identifier={FhirPatientMapper.NhsNumberSystem}|{{NHS number}}.";

    public SearchFhirPatientsValidator()
    {
        RuleFor(x => x.Identifier)
            .Must(identifier => !string.IsNullOrWhiteSpace(identifier) && FhirIdentifierSearch.Parse(identifier).Value.Length > 0)
            .WithMessage(IdentifierRequired)
            .OverridePropertyName("identifier");

        // Only a search that names the NHS number's system is checked as an NHS number. A value with no system matches in
        // any system (FHIR's token search), so one that isn't an NHS number just matches nothing.
        RuleFor(x => FhirIdentifierSearch.Parse(x.Identifier ?? string.Empty).Value)
            .NhsNumber()
            .When(x => !string.IsNullOrWhiteSpace(x.Identifier)
                       && FhirIdentifierSearch.Parse(x.Identifier) is { Value.Length: > 0, NamesNhsNumberSystem: true })
            .OverridePropertyName("identifier");
    }
}
