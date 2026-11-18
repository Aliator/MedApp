using MedApp.Contracts.Activity.Responses;
using MedApp.Domain.Activity;

namespace MedApp.Application.Activity.Common;

/// <summary>Activity log entries as the API returns them: the action by its name, and the patient by their name now.</summary>
public static class ActivityEntryMapper
{
    /// <summary>Shown in place of the name of a patient who has since been deleted.</summary>
    public const string DeletedPatient = "Deleted patient";

    public static ActivityEntryResponse ToResponse(ActivityEntry entry) =>
        ToResponse(entry, new Dictionary<Guid, string>());

    /// <summary>An entry, naming its patient from <paramref name="patientNames"/> (by ID).</summary>
    public static ActivityEntryResponse ToResponse(ActivityEntry entry, IReadOnlyDictionary<Guid, string> patientNames) => new()
    {
        Id = entry.Id,
        AtUtc = entry.AtUtc,
        Action = entry.Action.ToString(),
        UserId = entry.UserId,
        Username = entry.Username,
        IpAddress = entry.IpAddress,
        PatientId = entry.PatientId,
        PatientName = entry.PatientId is { } id
            ? patientNames.TryGetValue(id, out var name) ? name : DeletedPatient
            : null,
        PathwayId = entry.PathwayId,
        TargetUsername = entry.TargetUsername,
        Details = entry.Details
    };
}
