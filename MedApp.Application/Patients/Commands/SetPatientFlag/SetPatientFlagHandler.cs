using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Patients.Commands.SetPatientFlag;

/// <summary>
/// Sets or clears a patient's restricted or disputed flag, recorded in the activity log with the reason. Setting a flag
/// that's already set replaces its reason; clearing one that isn't set changes nothing. An anonymised record can't be
/// flagged (409). The flags don't count as a change to the record, so they don't move its retention date.
/// </summary>
public sealed class SetPatientFlagHandler(IPatientRepository repository, IActivityLog activityLog, TimeProvider timeProvider)
    : IRequestHandler<SetPatientFlagCommand, PatientResponse>
{
    public async Task<PatientResponse> Handle(SetPatientFlagCommand request, CancellationToken cancellationToken)
    {
        var patient = await repository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        if (PatientRules.IsAnonymised(patient))
            throw new ConflictException(PatientRules.AnonymisedMessage);

        var isSet = request.Flag == PatientFlag.Restricted
            ? patient.RestrictedAtUtc is not null
            : patient.DisputedAtUtc is not null;

        if (!request.Set && !isSet)
            return PatientMapper.ToResponse(patient);

        // Restricting freezes the procedures under way (all but blocking and cancelling), so the admin must say they know.
        // The message counts the open appointments too, which freeze the same way (all but cancelling).
        if (request is { Flag: PatientFlag.Restricted, Set: true, ConfirmOpenProcedures: false })
        {
            var underWay = await repository.CountProceduresUnderWayAsync(patient.Id, cancellationToken);
            if (underWay > 0)
            {
                var openAppointments = await repository.CountOpenAppointmentsAsync(patient.Id, cancellationToken);
                throw new ValidationException([
                    new ValidationFailure(nameof(request.ConfirmOpenProcedures), ConfirmationMessage(underWay, openAppointments))
                ]);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reason = request.Reason?.Trim();

        switch (request.Flag, request.Set)
        {
            case (PatientFlag.Restricted, true):
                patient.RestrictedAtUtc = now;
                patient.RestrictionReason = reason;
                break;
            case (PatientFlag.Restricted, false):
                patient.RestrictedAtUtc = null;
                patient.RestrictionReason = null;
                break;
            case (PatientFlag.Disputed, true):
                patient.DisputedAtUtc = now;
                patient.DisputeReason = reason;
                break;
            default:
                patient.DisputedAtUtc = null;
                patient.DisputeReason = null;
                break;
        }

        var action = (request.Flag, request.Set) switch
        {
            (PatientFlag.Restricted, true) => ActivityAction.PatientRestricted,
            (PatientFlag.Restricted, false) => ActivityAction.PatientRestrictionLifted,
            (PatientFlag.Disputed, true) => ActivityAction.PatientDisputed,
            _ => ActivityAction.PatientDisputeCleared
        };

        await repository.UpdateAsync(
            patient,
            activityLog.Entry(action, patient.Id, new { reason = string.IsNullOrEmpty(reason) ? null : reason }),
            cancellationToken);

        return PatientMapper.ToResponse(patient);
    }

    private static string ConfirmationMessage(int underWay, int openAppointments)
    {
        var procedures = $"{underWay} procedure{(underWay == 1 ? "" : "s")} under way";
        return openAppointments == 0
            ? $"This patient has {procedures}. Confirm that work on {(underWay == 1 ? "it" : "them")} stops while the record " +
              "is restricted (blocking and cancelling stay possible)."
            : $"This patient has {procedures} and {openAppointments} open appointment{(openAppointments == 1 ? "" : "s")}. " +
              "Confirm that work on them stops while the record is restricted (blocking and cancelling procedures, and " +
              "cancelling appointments, stay possible).";
    }
}
