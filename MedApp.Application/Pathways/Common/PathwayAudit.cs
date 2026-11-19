using System.Text.Json;
using System.Text.Json.Serialization;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

public static class PathwayAudit
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>An audit entry for a change to a pathway (and one of its stages), with its details as camelCase JSON.</summary>
    public static PatientPathwayAuditEntry Entry(
        PatientPathway pathway,
        PathwayActor actor,
        PatientPathwayAuditAction action,
        DateTime nowUtc,
        PatientPathwayStage? stage = null,
        object? details = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = pathway.Id,
        PathwayTitle = pathway.Title,
        PatientId = pathway.PatientId,
        TeamId = pathway.TeamId,
        StageId = stage?.Id,
        StageName = stage?.Name,
        Action = action,
        UserId = actor.UserId,
        Username = actor.Username,
        AtUtc = nowUtc,
        Details = Details(details)
    };

    /// <summary>
    /// Details as compact camelCase JSON, leaving out nulls (<c>{}</c> for none). The pathway audit log and
    /// appointments' history both write them this way, which the timeline's markers rely on.
    /// </summary>
    public static string Details(object? details) => details is null ? "{}" : JsonSerializer.Serialize(details, Json);
}
