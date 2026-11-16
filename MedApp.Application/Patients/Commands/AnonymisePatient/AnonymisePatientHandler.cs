using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

/// <summary>
/// Anonymises a patient's record across every table in one save, with an activity log entry giving the reason (one of
/// <see cref="AnonymisationReason"/>, so it holds no patient details) and an optional note
/// (<see cref="PatientAnonymiser"/>). Refused (409) while any of their procedures is under way, while the record is
/// restricted, or if it's already anonymised; and (400) unless the confirmation is the patient's full name.
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

        var underWay = record.Procedures.Count(p => !ProcedureWorkflow.IsClosed(p.Status));
        if (underWay > 0)
            throw new ConflictException(
                $"This patient has {underWay} procedure{(underWay == 1 ? "" : "s")} under way, so their record can't be anonymised " +
                "until they're completed, cancelled or archived.");

        if (!SameName(request.Confirmation, PatientMapper.FullName(patient)))
            throw new ValidationException(
                [new ValidationFailure(nameof(request.Confirmation), "Type the patient's full name exactly as shown to confirm.")]);

        PatientAnonymiser.Apply(record, timeProvider.GetUtcNow().UtcDateTime, activityLog.Actor);

        await repository.SaveAsync(
            activityLog.Entry(ActivityAction.PatientAnonymised, patient.Id, new
            {
                reason = ProcedureEnums.Parse<AnonymisationReason>(request.Reason).ToString(),
                note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                procedureCount = record.Procedures.Count
            }),
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
