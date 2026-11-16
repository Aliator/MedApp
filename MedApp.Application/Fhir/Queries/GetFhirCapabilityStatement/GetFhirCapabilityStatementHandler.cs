using MedApp.Application.Fhir.Common;
using MedApp.Contracts.Fhir;
using MediatR;

namespace MedApp.Application.Fhir.Queries.GetFhirCapabilityStatement;

public sealed class GetFhirCapabilityStatementHandler(TimeProvider timeProvider)
    : IRequestHandler<GetFhirCapabilityStatementQuery, FhirCapabilityStatement>
{
    public Task<FhirCapabilityStatement> Handle(GetFhirCapabilityStatementQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(FhirCapability.Statement(timeProvider.GetUtcNow().UtcDateTime));
}
