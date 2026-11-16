using MedApp.Application.Activity.Common;
using MedApp.Application.Procedures.Audit.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>A patient's whole record (<see cref="PatientRecord"/>) as a subject access export.</summary>
public static class PatientExportMapper
{
    /// <summary>Every user the export names: on procedures (assignees, assigners, stage workers) and as notification recipients.</summary>
    public static IEnumerable<Guid> UserIds(PatientRecord record) =>
        record.Procedures.SelectMany(ProcedureMapper.UserIds)
            .Concat(record.Notifications.Select(n => n.UserId))
            .Distinct();

    public static PatientExportResponse ToResponse(
        PatientRecord record,
        IReadOnlyDictionary<Guid, string> usernames,
        DateTime nowUtc,
        string exportedBy)
    {
        var patientName = PatientMapper.FullName(record.Patient);
        var patientNames = new Dictionary<Guid, string> { [record.Patient.Id] = patientName };

        return new PatientExportResponse
        {
            ExportedAtUtc = nowUtc,
            ExportedBy = exportedBy,
            Patient = ToExported(record.Patient),
            Procedures = record.Procedures
                .OrderBy(p => p.CreatedAt)
                .Select(p => ProcedureMapper.ToResponse(p, usernames, nowUtc, patientName))
                .ToList(),
            ProcedureHistory = record.ProcedureHistory.Select(AuditEntryMapper.ToResponse).ToList(),
            AccessLog = record.Activity.Select(e => ActivityEntryMapper.ToResponse(e, patientNames)).ToList(),
            Notifications = record.Notifications
                .Select(n => new ExportedNotificationResponse
                {
                    Id = n.Id,
                    Kind = n.Kind.ToString(),
                    Username = usernames.TryGetValue(n.UserId, out var name) ? name : ProcedureMapper.DeletedUser,
                    Title = n.Title,
                    Message = n.Message,
                    ProcedureId = n.ProcedureId,
                    CreatedAtUtc = n.CreatedAtUtc,
                    ReadAtUtc = n.ReadAtUtc
                })
                .ToList()
        };
    }

    public static ExportedPatientResponse ToExported(Patient patient) => new()
    {
        Id = patient.Id,
        FirstName = patient.FirstName,
        LastName = patient.LastName,
        DateOfBirth = patient.DateOfBirth,
        Email = patient.Email,
        CreatedAt = patient.CreatedAt,
        LastUpdated = patient.LastUpdated,
        RestrictedAtUtc = patient.RestrictedAtUtc,
        RestrictionReason = patient.RestrictionReason,
        DisputedAtUtc = patient.DisputedAtUtc,
        DisputeReason = patient.DisputeReason,
        AnonymisedAtUtc = patient.AnonymisedAtUtc,
        RetentionKeptUntil = patient.RetentionKeptUntil,
        RetentionKeptReason = patient.RetentionKeptReason
    };
}
