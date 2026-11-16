using MedApp.Contracts.Fhir;
using MediatR;

namespace MedApp.Application.Fhir.Queries.GetFhirPatient;

/// <summary>A patient as a FHIR Patient resource (UK Core). The ID is MedApp's patient ID; anything else is a 404.</summary>
public sealed record GetFhirPatientQuery(string Id) : IRequest<FhirPatient>;
