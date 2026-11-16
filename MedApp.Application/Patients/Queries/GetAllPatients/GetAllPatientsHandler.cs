using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Queries.GetAllPatients;

/// <summary>
/// Every patient (only the deprecated Blazor client asks). The activity log records it as one search for all of them,
/// not an entry per patient.
/// </summary>
public sealed class GetAllPatientsHandler(IPatientRepository repository, IActivityLog activityLog)
    : IRequestHandler<GetAllPatientsQuery, IReadOnlyList<PatientResponse>>
{
    public async Task<IReadOnlyList<PatientResponse>> Handle(GetAllPatientsQuery request, CancellationToken cancellationToken)
    {
        var patients = (await repository.GetAllAsync(cancellationToken)).ToList();

        await activityLog.RecordAsync(
            activityLog.Entry(ActivityAction.PatientsSearched, details: new { filters = new[] { "all" }, resultCount = patients.Count }),
            cancellationToken);

        return patients.Select(PatientMapper.ToResponse).ToList();
    }
}
