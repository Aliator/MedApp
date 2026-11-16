using FluentValidation;
using MedApp.Application.Fhir.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Patients;

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

        // A number in another system can't match anything, so only an NHS number search is checked as one.
        RuleFor(x => x.Identifier)
            .Must(identifier => NhsNumbers.IsValid(FhirIdentifierSearch.Parse(identifier!).Value))
            .WithMessage(identifier => NhsNumbers.IsTenDigits(FhirIdentifierSearch.Parse(identifier.Identifier!).Value)
                ? NhsNumberRules.WrongCheckDigit
                : NhsNumberRules.NotTenDigits)
            .When(x => !string.IsNullOrWhiteSpace(x.Identifier)
                       && FhirIdentifierSearch.Parse(x.Identifier) is { Value.Length: > 0, IsForNhsNumber: true })
            .OverridePropertyName("identifier");
    }
}
