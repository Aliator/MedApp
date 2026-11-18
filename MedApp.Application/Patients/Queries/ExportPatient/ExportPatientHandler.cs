using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Patients.Queries.ExportPatient;

/// <summary>
/// A subject access export: the patient's whole record from every table (<see cref="PatientRecord"/>), with usernames
/// looked up. The export is recorded in the activity log first, so the access log it contains includes it.
/// </summary>
public sealed class ExportPatientHandler(
    IPatientRecordRepository repository,
    IIdentityReadService identity,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<ExportPatientQuery, PatientExportResponse>
{
    public async Task<PatientExportResponse> Handle(ExportPatientQuery request, CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(request.PatientId, forUpdate: false, cancellationToken)
                     ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        var entry = activityLog.Entry(ActivityAction.PatientExported, record.Patient.Id);
        await activityLog.RecordAsync(entry, cancellationToken);

        // Everything else as read: a with expression copies every list, so a new one can't be left out.
        var withExport = record with { Activity = [.. record.Activity, entry] };

        var usernames = await identity.GetUsernamesByIdAsync(PatientExportMapper.UserIds(withExport), cancellationToken);

        return PatientExportMapper.ToResponse(
            withExport, usernames, timeProvider.GetUtcNow().UtcDateTime, activityLog.Actor.Username);
    }
}
