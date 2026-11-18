using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

/// <summary>
/// Anonymises a patient's record across every table in one save, with an activity log entry giving the reason (one of
/// <see cref="AnonymisationReason"/>, so it holds no patient details) and an optional note
/// (<see cref="PatientAnonymiser"/>). Refused (409) while any of their pathways is under way, while any of their
/// appointments is open (Requested or Booked), while any of their referrals awaits triage, while a data request is open, while the record is
/// restricted, or if it's already anonymised; and (400)
/// unless the confirmation is the patient's full name.
/// </summary>
public sealed partial class AnonymisePatientHandler(
    IPatientRecordRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<AnonymisePatientCommand, PatientResponse>
{
    public async Task<PatientResponse> Handle(AnonymisePatientCommand request, CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(request.PatientId, forUpdate: true, cancellationToken)
                     ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");
        var patient = record.Patient;

        if (PatientRules.IsAnonymised(patient))
            throw new ConflictException("This patient's record has already been anonymised.");

        if (PatientRules.IsRestricted(patient))
            throw new ConflictException("This patient's record is restricted. Lift the restriction before anonymising it.");

        var underWay = record.Pathways.Count(p => !PathwayWorkflow.IsClosed(p.Status));
        if (underWay > 0)
            throw new ConflictException(
                $"This patient has {underWay} pathway{(underWay == 1 ? "" : "s")} under way, so their record can't be anonymised " +
                "until they're discharged, cancelled or archived.");

        var openAppointments = record.Appointments.Count(a => AppointmentWorkflow.IsOpen(a.Status));
        if (openAppointments > 0)
            throw new ConflictException(
                $"This patient has {openAppointments} open appointment{(openAppointments == 1 ? "" : "s")} (needing booking or " +
                $"booked), so their record can't be anonymised. Cancel {(openAppointments == 1 ? "it" : "them")}, or record " +
                $"{(openAppointments == 1 ? "its" : "their")} outcome, first.");

        var awaitingTriage = record.Referrals.Count(r => ReferralWorkflow.IsOpen(r.Status));
        if (awaitingTriage > 0)
            throw new ConflictException(
                $"This patient has {awaitingTriage} referral{(awaitingTriage == 1 ? "" : "s")} awaiting triage, so their " +
                $"record can't be anonymised until {(awaitingTriage == 1 ? "it's" : "they're")} triaged or withdrawn.");

        // A letter still being written, awaiting approval or not yet sent to everyone is care still under way.
        var openLetters = record.Letters.Count(l => l.Status is LetterStatus.Draft or LetterStatus.AwaitingApproval
                                                    || (l.Status == LetterStatus.Approved && !l.IsSentToEveryone));
        if (openLetters > 0)
            throw new ConflictException(
                $"This patient has {openLetters} letter{(openLetters == 1 ? "" : "s")} not yet sent to everyone (a draft, " +
                "awaiting approval, or approved and still to send), so their record can't be anonymised until " +
                $"{(openLetters == 1 ? "it's" : "they're")} sent, discarded or withdrawn.");

        var openSpecimens = record.Specimens.Count(s => SpecimenWorkflow.IsOpen(s.Status));
        if (openSpecimens > 0)
            throw new ConflictException(
                $"This patient has {openSpecimens} specimen{(openSpecimens == 1 ? "" : "s")} still with the lab (taken, sent or " +
                $"received), so their record can't be anonymised until {(openSpecimens == 1 ? "it's" : "they're")} reported, " +
                "rejected, lost or entered in error.");

        var awaitingResults = record.Results.Count(r => r.IsAwaitingSignOff);
        if (awaitingResults > 0)
            throw new ConflictException(
                $"This patient has {awaitingResults} result{(awaitingResults == 1 ? "" : "s")} awaiting sign-off, so their " +
                $"record can't be anonymised until {(awaitingResults == 1 ? "it's" : "they're")} signed off or marked entered in error.");

        // An open data request must be answered from the record as it is: altering it now could hide what the patient
        // asked to see (Data Protection Act 2018, section 173).
        var openDataRequests = record.DataRequests.Count(d => d.IsOpen);
        if (openDataRequests > 0)
            throw new ConflictException(
                $"This patient has {openDataRequests} open data request{(openDataRequests == 1 ? "" : "s")}, so their record " +
                $"can't be anonymised until {(openDataRequests == 1 ? "it's" : "they're")} sent, refused or withdrawn.");

        if (!SameName(request.Confirmation, PatientMapper.FullName(patient)))
            throw new ValidationException(
                [new ValidationFailure(nameof(request.Confirmation), "Type the patient's full name exactly as shown to confirm.")]);

        var fileDeletions = PatientAnonymiser.Apply(record, timeProvider.GetUtcNow().UtcDateTime, activityLog.Actor);

        await repository.SaveAsync(
            activityLog.Entry(ActivityAction.PatientAnonymised, patient.Id, new
            {
                reason = PathwayEnums.Parse<AnonymisationReason>(request.Reason).ToString(),
                note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                pathwayCount = record.Pathways.Count
            }),
            fileDeletions,
            cancellationToken);

        return PatientMapper.ToResponse(patient);
    }

    /// <summary>Whether two names match, ignoring case and extra spaces.</summary>
    private static bool SameName(string typed, string name) =>
        string.Equals(Normalise(typed), Normalise(name), StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string text) => Spaces().Replace(text.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
