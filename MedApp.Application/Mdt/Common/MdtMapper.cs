using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;

namespace MedApp.Application.Mdt.Common;

/// <summary>MDT items and MDTs as the API returns them.</summary>
public static class MdtMapper
{
    /// <summary>
    /// An item as <paramref name="viewer"/> sees it (its <c>Can…</c> flags follow their permissions), read with its
    /// patient, pathway, team, meeting and who was present.
    /// </summary>
    public static MdtItemResponse ToResponse(MdtItem item, PathwayActor viewer, DateTime nowUtc) => new()
    {
        Id = item.Id,
        Status = item.Status.ToString(),
        Patient = PathwayMapper.ToPatient(item.Patient),
        PathwayId = item.PathwayId,
        PathwayTitle = item.Pathway?.Title ?? string.Empty,
        TeamId = item.TeamId,
        TeamName = item.Team?.Name ?? string.Empty,
        MeetingId = item.MeetingId,
        MeetingTitle = item.Meeting?.Title,
        MeetingStartUtc = item.Meeting?.StartUtc,
        MeetingCancelledStartUtc = item.MeetingId is null ? item.MeetingCancelledStartUtc : null,
        Question = item.Question,
        ListedByUsername = item.ListedByUsername,
        ListedAtUtc = item.ListedAtUtc,
        Outcome = item.Outcome,
        OutcomeByUsername = item.OutcomeByUsername,
        OutcomeAtUtc = item.OutcomeAtUtc,
        Present = item.Attendees.Select(a => a.Username).OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList(),
        WithdrawnByUsername = item.WithdrawnByUsername,
        WithdrawnAtUtc = item.WithdrawnAtUtc,
        Version = item.Version,
        CanWithdraw = MdtRules.CanWithdraw(item, viewer),
        CanDefer = MdtRules.CanDefer(item, viewer),
        CanRecordOutcome = MdtRules.CanRecordOutcome(item, viewer, nowUtc),
        ReadOnlyReason = PatientRules.WhyCantChange(item.Patient)
    };

    /// <summary>An MDT with its counts, and its attendees by username (a deleted account's left out).</summary>
    public static MdtMeetingResponse ToResponse(MdtMeetingRow row, IReadOnlyDictionary<Guid, string> usernames) => new()
    {
        Id = row.Meeting.Id,
        Title = row.Meeting.Title,
        StartUtc = row.Meeting.StartUtc,
        EndUtc = row.Meeting.EndUtc,
        TeamId = row.Meeting.TeamId ?? Guid.Empty,
        TeamName = row.TeamName,
        OpenItemCount = row.OpenItemCount,
        ItemCount = row.ItemCount,
        Attendees = row.Meeting.Attendees
            .Where(a => usernames.ContainsKey(a.StaffUserId))
            .Select(a => new MdtPersonResponse { UserId = a.StaffUserId, Username = usernames[a.StaffUserId] })
            .OrderBy(a => a.Username, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.UserId)
            .ToList()
    };
}
