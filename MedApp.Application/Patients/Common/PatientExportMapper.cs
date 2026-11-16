using MedApp.Application.Attachments.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// A patient's whole record (<see cref="PatientRecord"/>) as a subject access export. Staff appear by username only:
/// their account IDs, network addresses and when they read a notification are the staff's data, so they're left out.
/// </summary>
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
        string Username(Guid? userId) =>
            userId is { } id ? usernames.TryGetValue(id, out var name) ? name : ProcedureMapper.DeletedUser : string.Empty;

        return new PatientExportResponse
        {
            ExportedAtUtc = nowUtc,
            ExportedBy = exportedBy,
            Patient = ToExported(record.Patient),
            Procedures = record.Procedures
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ExportedProcedureResponse
                {
                    Id = p.Id,
                    Title = p.Title,
                    TemplateName = p.TemplateName,
                    Status = p.Status.ToString(),
                    Priority = p.Priority.ToString(),
                    DueDate = DueDates.ToUkDate(p.DueDateUtc),
                    CreatedAt = p.CreatedAt,
                    LastUpdated = p.LastUpdated,
                    Notes = p.Notes,
                    AssignedTo = p.Assignments.OrderBy(a => a.AssignedAtUtc).Select(a => Username(a.UserId)).ToList(),
                    Stages = p.Stages
                        .OrderBy(s => s.StageOrder)
                        .Select(s => new ExportedStageResponse
                        {
                            Order = s.StageOrder,
                            Name = s.Name,
                            Notes = s.Notes,
                            StartedAtUtc = s.StartedAtUtc,
                            StartedBy = s.StartedByUserId is null ? null : Username(s.StartedByUserId),
                            FinishedAtUtc = s.CompletedAtUtc,
                            FinishedBy = s.CompletedByUserId is null ? null : Username(s.CompletedByUserId)
                        })
                        .ToList()
                })
                .ToList(),
            // Staff by the username recorded at the time; never their account IDs or network addresses.
            ProcedureHistory = record.ProcedureHistory
                .Select(e => new ExportedHistoryEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    ProcedureId = e.PatientProcedureId,
                    ProcedureTitle = e.ProcedureTitle,
                    StageName = e.StageName,
                    Details = e.Details
                })
                .ToList(),
            AccessLog = record.Activity
                .Select(e => new ExportedAccessEntryResponse
                {
                    AtUtc = e.AtUtc,
                    Action = e.Action.ToString(),
                    Username = e.Username,
                    ProcedureId = e.ProcedureId,
                    Details = e.Details
                })
                .ToList(),
            Notifications = record.Notifications
                .Select(n => new ExportedNotificationResponse
                {
                    CreatedAtUtc = n.CreatedAtUtc,
                    Kind = n.Kind.ToString(),
                    Username = Username(n.UserId),
                    Title = n.Title,
                    Message = n.Message,
                    ProcedureId = n.ProcedureId
                })
                .ToList(),
            // Who by the username recorded at the time, as the procedure history does.
            Attachments = record.Attachments
                .Select(a => new ExportedAttachmentResponse
                {
                    Id = a.Id,
                    AttachedTo = AttachmentTarget.KindOf(a).ToString(),
                    ProcedureId = a.ProcedureId,
                    StageName = a.StageId is { } stageId
                        ? record.Procedures.SelectMany(p => p.Stages).FirstOrDefault(s => s.Id == stageId)?.Name
                        : null,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    TypeName = AttachmentFileTypes.FromContentType(a.ContentType)?.Name ?? a.ContentType,
                    SizeBytes = a.SizeBytes,
                    Sha256 = a.Sha256,
                    ScanStatus = a.ScanStatus.ToString(),
                    UploadedBy = a.UploadedByUsername,
                    UploadedAtUtc = a.UploadedAtUtc,
                    DeletedAtUtc = a.DeletedAtUtc,
                    DeletedBy = a.DeletedByUsername
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
        NhsNumber = patient.NhsNumber,
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
