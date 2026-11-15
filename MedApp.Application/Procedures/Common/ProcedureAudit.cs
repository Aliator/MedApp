using System.Text.Json;
using System.Text.Json.Serialization;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureAudit
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>An audit entry for a change to a procedure (and one of its stages), with its details as camelCase JSON.</summary>
    public static PatientProcedureAuditEntry Entry(
        PatientProcedure procedure,
        ProcedureActor actor,
        PatientProcedureAuditAction action,
        DateTime nowUtc,
        PatientProcedureStage? stage = null,
        object? details = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientProcedureId = procedure.Id,
        ProcedureTitle = procedure.Title,
        PatientId = procedure.PatientId,
        StageId = stage?.Id,
        StageName = stage?.Name,
        Action = action,
        UserId = actor.UserId,
        Username = actor.Username,
        AtUtc = nowUtc,
        Details = details is null ? "{}" : JsonSerializer.Serialize(details, Json)
    };
}
