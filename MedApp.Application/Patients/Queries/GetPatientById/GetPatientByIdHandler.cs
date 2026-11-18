using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MediatR;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Queries.GetPatientById;

/// <summary>A patient's record. Opening it is recorded in the activity log (once per person every few minutes).</summary>
public sealed class GetPatientByIdHandler(
    IPatientRepository repository,
    IPracticeRepository practices,
    IActivityLog activityLog)
    : IRequestHandler<GetPatientByIdQuery, PatientResponse?>
{
    public async Task<PatientResponse?> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (patient is null)
            return null;

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [patient.Id], cancellationToken);

        var practice = patient.PracticeId is { } practiceId ? await practices.GetAsync(practiceId, cancellationToken) : null;
        return PatientMapper.ToResponse(patient, practice);
    }
}
