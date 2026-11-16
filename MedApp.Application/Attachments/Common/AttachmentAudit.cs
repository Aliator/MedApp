using System.Text.Json;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Audit entries for attached files, in the procedure audit log beside every other change, so they're on the
/// patient's timeline and in the admins' log. A patient's own file has no procedure
/// (<see cref="PatientProcedureAuditEntry.NoProcedure"/>). The details hold the file's ID, name (free text: only
/// admins see it on the timeline), type, size and what it's attached to. Attaching a file isn't a change to the
/// procedure, so these don't go through <see cref="ProcedureChange"/> and don't touch its LastUpdated.
/// </summary>
public static class AttachmentAudit
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PatientProcedureAuditEntry Entry(
        Attachment attachment,
        AttachmentTarget target,
        ProcedureActor actor,
        PatientProcedureAuditAction action,
        DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PatientProcedureId = target.Procedure?.Id ?? PatientProcedureAuditEntry.NoProcedure,
        ProcedureTitle = target.Procedure?.Title ?? string.Empty,
        PatientId = attachment.PatientId,
        StageId = target.Stage?.Id,
        StageName = target.Stage?.Name,
        Action = action,
        UserId = actor.UserId,
        Username = actor.Username,
        AtUtc = nowUtc,
        Details = JsonSerializer.Serialize(
            new
            {
                attachmentId = attachment.Id,
                fileName = attachment.FileName,
                contentType = attachment.ContentType,
                size = attachment.SizeBytes,
                attachedTo = AttachmentTarget.KindOf(attachment).ToString(),
            },
            Json)
    };
}
