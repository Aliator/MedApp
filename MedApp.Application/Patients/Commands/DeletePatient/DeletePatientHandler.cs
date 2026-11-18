using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;

namespace MedApp.Application.Patients.Commands.DeletePatient;

public sealed class DeletePatientHandler(
    IPatientRepository repository,
    IActivityLog activityLog,
    IAttachmentRepository attachments,
    MedApp.Application.Results.Repositories.IResultRepository results,
    MedApp.Application.DataRequests.Repositories.IDataRequestRepository dataRequests)
    : IRequestHandler<DeletePatientCommand, bool>
{
    public async Task<bool> Handle(DeletePatientCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.Id, cancellationToken);

        if (patient is null)
            return false;

        // A restricted record can't be changed, and deleting it is the biggest change of all (409).
        if (PatientRules.IsRestricted(patient))
            throw new ConflictException(PatientRules.RestrictedMessage);

        // Pathways keep a patient's medical history, so a patient who has any can't be deleted (409).
        var pathwayCount = await repository.CountPathwaysAsync(request.Id, cancellationToken);
        if (pathwayCount > 0)
            throw new ConflictException(
                $"This patient has {pathwayCount} pathway{(pathwayCount == 1 ? "" : "s")}, so they can't be deleted.");

        // So do appointments, whatever their status.
        var appointmentCount = await repository.CountAppointmentsAsync(request.Id, cancellationToken);
        if (appointmentCount > 0)
            throw new ConflictException(
                $"This patient has {appointmentCount} appointment{(appointmentCount == 1 ? "" : "s")}, so they can't be deleted.");

        // So do referrals: what was asked for, by whom, and what was decided.
        var referralCount = await repository.CountReferralsAsync(request.Id, cancellationToken);
        if (referralCount > 0)
            throw new ConflictException(
                $"This patient has {referralCount} referral{(referralCount == 1 ? "" : "s")}, so they can't be deleted.");

        // So do results (an external one entered in error can outlive its file).
        var resultCount = await results.CountForPatientAsync(request.Id, cancellationToken);
        if (resultCount > 0)
            throw new ConflictException(
                $"This patient has {resultCount} result{(resultCount == 1 ? "" : "s")}, so they can't be deleted.");

        // So do data requests: the record of how the patient's right of access was answered.
        var dataRequestCount = await dataRequests.CountForPatientAsync(request.Id, cancellationToken);
        if (dataRequestCount > 0)
            throw new ConflictException(
                $"This patient has {dataRequestCount} data request{(dataRequestCount == 1 ? "" : "s")}, so they can't be deleted.");

        // Nor can one with files attached: they must be deleted first (which is audited).
        var fileCount = await attachments.CountForPatientAsync(request.Id, cancellationToken);
        if (fileCount > 0)
            throw new ConflictException(
                $"This patient has {fileCount} file{(fileCount == 1 ? "" : "s")} attached, so they can't be deleted. Delete the files first.");

        await repository.DeleteAsync(request.Id, activityLog.Entry(ActivityAction.PatientDeleted, request.Id), cancellationToken);

        return true;
    }
}
