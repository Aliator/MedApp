using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Fhir.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Fhir;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Fhir.Queries.GetFhirPatient;

/// <summary>
/// Reads a patient through the FHIR API. Recorded in the activity log as the patient's page is
/// (<see cref="ActivityAction.PatientViewed"/>, once per person every few minutes). An unknown ID is a 404.
/// </summary>
public sealed class GetFhirPatientHandler(IPatientRepository repository, IActivityLog activityLog)
    : IRequestHandler<GetFhirPatientQuery, FhirPatient>
{
    public async Task<FhirPatient> Handle(GetFhirPatientQuery request, CancellationToken cancellationToken)
    {
        var patient = Guid.TryParse(request.Id, out var id)
            ? await repository.GetByIdAsync(id, cancellationToken)
            : null;

        if (patient is null)
            throw new NotFoundException($"Patient '{request.Id}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [patient.Id], cancellationToken);

        return FhirPatientMapper.ToResource(patient);
    }
}
