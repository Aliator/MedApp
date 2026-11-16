using MedApp.Contracts.Fhir;
using MediatR;

namespace MedApp.Application.Fhir.Queries.SearchFhirPatients;

/// <summary>
/// Searches patients by identifier, the only search the FHIR API offers:
/// <c>identifier=https://fhir.nhs.uk/Id/nhs-number|{number}</c>. <paramref name="BaseUrl"/> is the FHIR API's address
/// (e.g. <c>https://host/api/fhir</c>), for each entry's full URL and the bundle's self link.
/// </summary>
public sealed record SearchFhirPatientsQuery(string? Identifier, string BaseUrl) : IRequest<FhirBundle>;
