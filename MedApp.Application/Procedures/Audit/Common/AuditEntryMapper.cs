using MedApp.Contracts.Procedures.Audit.Responses;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Audit.Common;

/// <summary>Audit log entries as the API returns them; the action by its name.</summary>
public static class AuditEntryMapper
{
    public static PatientProcedureAuditEntryResponse ToResponse(PatientProcedureAuditEntry entry) => new()
    {
        Id = entry.Id,
        PatientProcedureId = entry.PatientProcedureId,
        ProcedureTitle = entry.ProcedureTitle,
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
