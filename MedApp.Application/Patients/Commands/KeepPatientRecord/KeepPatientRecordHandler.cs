using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Patients.Commands.KeepPatientRecord;

/// <summary>
/// Records an admin's decision to keep a record past its retention period, until a review date, with a reason, in the
/// activity log. It's a decision about the record, not a change to it, so it doesn't move the retention date, and it's
/// allowed on a restricted record.
/// </summary>
public sealed class KeepPatientRecordHandler(IPatientRepository repository, IActivityLog activityLog)
    : IRequestHandler<KeepPatientRecordCommand>
{
    public async Task Handle(KeepPatientRecordCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        if (PatientRules.IsAnonymised(patient))
            throw new ConflictException("This patient's record has been anonymised, so it's no longer due for review.");

        var reason = request.Reason.Trim();
        patient.RetentionKeptUntil = request.KeepUntil;
        patient.RetentionKeptReason = reason;

        await repository.UpdateAsync(
            patient,
            activityLog.Entry(ActivityAction.RetentionKept, patient.Id, new { reason, keepUntil = request.KeepUntil }),
            cancellationToken);
    }
}
