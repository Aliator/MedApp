using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Audit entries for attached files, in the pathway audit log beside every other change, so they're on the
/// patient's timeline and in the admins' log. A patient's own file has no pathway
/// (<see cref="PatientPathwayAuditEntry.NoPathway"/>). The details hold the file's ID, name (free text: only
/// admins see it on the timeline), type, size and what it's attached to. Attaching a file isn't a change to the
/// pathway, so these don't go through <see cref="PathwayChange"/> and don't touch its LastUpdated.
/// </summary>
public static class AttachmentAudit
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PatientPathwayAuditEntry Entry(
        Attachment attachment,
        AttachmentTarget target,
        PathwayActor actor,
        PatientPathwayAuditAction action,
        DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = target.Pathway?.Id ?? PatientPathwayAuditEntry.NoPathway,
        PathwayTitle = target.Pathway?.Title ?? string.Empty,
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
