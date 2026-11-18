using MedApp.Contracts.Pathways.Audit.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Audit.Common;

/// <summary>Audit log entries as the API returns them; the action by its name.</summary>
public static class AuditEntryMapper
{
    public static PatientPathwayAuditEntryResponse ToResponse(PatientPathwayAuditEntry entry) => new()
    {
        Id = entry.Id,
        PatientPathwayId = entry.PatientPathwayId,
        PathwayTitle = entry.PathwayTitle,
        PatientId = entry.PatientId,
        StageId = entry.StageId,
        StageName = entry.StageName,
        Action = entry.Action.ToString(),
        UserId = entry.UserId,
        Username = entry.Username,
        AtUtc = entry.AtUtc,
        Details = entry.Details
    };
}
