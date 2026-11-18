using MedApp.Application.Mdt.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Mdt.Repositories;

/// <summary>
/// MDT items and the MDTs (meetings marked as a team's) they're listed for. Each change saves its audit entry in
/// the same save. Items are read with their patient, pathway (and its assignees), team, meeting (and its
/// attendees) and who was present.
/// </summary>
public interface IMdtItemRepository
{
    /// <summary>An item with everything its response shows, read-only.</summary>
    Task<MdtItem?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>An item with everything its response shows, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<MdtItem?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A meeting's items, in the order they were listed (then by ID).</summary>
    Task<IReadOnlyList<MdtItem>> GetForMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Open items whose meeting was cancelled, oldest listed first, in one team or all.</summary>
    Task<IReadOnlyList<MdtItem>> GetAwaitingMeetingAsync(Guid? teamId, CancellationToken ct);

    /// <summary>A pathway's items, newest listed first.</summary>
    Task<IReadOnlyList<MdtItem>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>Whether the pathway is on that meeting's list, still to be discussed.</summary>
    Task<bool> IsOpenOnMeetingAsync(Guid pathwayId, Guid meetingId, CancellationToken ct);

    /// <summary>A meeting with its attendees, read-only; null if there's none.</summary>
    Task<Meeting?> GetMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>MDTs starting in the window, in one team or all, by start, with their counts and team's name.</summary>
    Task<IReadOnlyList<MdtMeetingRow>> GetMeetingsAsync(Guid? teamId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>One MDT with its counts and team's name; null if there's no such meeting or it isn't an MDT.</summary>
    Task<MdtMeetingRow?> GetMeetingRowAsync(Guid meetingId, CancellationToken ct);

    /// <summary>How many items on a meeting are still to be discussed, and how many have an outcome.</summary>
    Task<(int Open, int Discussed)> CountOnMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Adds a new item and its audit entry.</summary>
    Task AddAsync(MdtItem item, PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>
    /// Saves changes to an item loaded with <see cref="GetForUpdateAsync"/>, with its audit entry; if someone else
    /// saved it since (its row version), a 409 (<see cref="MdtRules.ChangedMeanwhileMessage"/>) and nothing is saved.
    /// </summary>
    Task SaveAsync(PatientPathwayAuditEntry audit, CancellationToken ct);
}
