using MedApp.Application.Activity.Common;
using MedApp.Application.Fhir.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Fhir;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MediatR;

namespace MedApp.Application.Fhir.Queries.SearchFhirPatients;

/// <summary>
/// Finds the patient with an NHS number, as a searchset Bundle (empty when there's none, or the search names another
/// identifier system, or a value with no system that isn't an NHS number). Recorded in the activity log as the patient search is: the search (its filter's name and how
/// many matched, never the number) and each patient found (<see cref="ActivityAction.PatientListed"/>).
/// </summary>
public sealed class SearchFhirPatientsHandler(IPatientRepository repository, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<SearchFhirPatientsQuery, FhirBundle>
{
    public async Task<FhirBundle> Handle(SearchFhirPatientsQuery request, CancellationToken cancellationToken)
    {
        var search = FhirIdentifierSearch.Parse(request.Identifier!);

        var nhsNumber = search.NhsNumberToMatch;

        var found = new List<Patient>();
        if (nhsNumber is not null && await repository.GetByNhsNumberAsync(nhsNumber, cancellationToken) is { } patient)
            found.Add(patient);

        // Named for what was searched: an NHS number, or some other identifier (which matches nothing).
        var filter = nhsNumber is not null ? "nhsNumber" : "identifier";
        await activityLog.RecordAsync(
            activityLog.Entry(
                ActivityAction.PatientsSearched,
                details: new { filters = new[] { filter }, resultCount = found.Count, via = "fhir" }),
            cancellationToken);
        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, found.Select(p => p.Id), cancellationToken);

        var baseUrl = request.BaseUrl.TrimEnd('/');
        var selfUrl = $"{baseUrl}/Patient?identifier={Uri.EscapeDataString(request.Identifier!)}";
        return FhirPatientMapper.ToSearchSet(found, baseUrl, selfUrl, timeProvider.GetUtcNow().UtcDateTime);
    }
}
