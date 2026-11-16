using MedApp.Contracts.Fhir;
using MediatR;

namespace MedApp.Application.Fhir.Queries.GetFhirCapabilityStatement;

/// <summary>What the FHIR API can do (<c>GET api/fhir/metadata</c>).</summary>
public sealed record GetFhirCapabilityStatementQuery : IRequest<FhirCapabilityStatement>;
