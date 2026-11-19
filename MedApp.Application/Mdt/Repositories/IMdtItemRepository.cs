using MedApp.Application.Common.Teams;
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

    /// <summary>
    /// A meeting's items the viewer may see (<see cref="MdtWalls.Visible"/>), in the order they were listed (then by ID).
    /// </summary>
    Task<IReadOnlyList<MdtItem>> GetForMeetingAsync(Guid meetingId, TeamScope scope, CancellationToken ct);

    /// <summary>
    /// Open items whose meeting was cancelled, of those the viewer may see, oldest listed first, in one team or all.
    /// </summary>
    Task<IReadOnlyList<MdtItem>> GetAwaitingMeetingAsync(Guid? teamId, TeamScope scope, CancellationToken ct);

    /// <summary>A pathway's items, newest listed first.</summary>
    Task<IReadOnlyList<MdtItem>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>Whether the pathway is on that meeting's list, still to be discussed.</summary>
    Task<bool> IsOpenOnMeetingAsync(Guid pathwayId, Guid meetingId, CancellationToken ct);

    /// <summary>A meeting with its attendees, read-only; null if there's none.</summary>
    Task<Meeting?> GetMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>
    /// MDTs starting in the window, in one team or all, by start, with their team's name and counts of the items the
    /// viewer may see.
    /// </summary>
    Task<IReadOnlyList<MdtMeetingRow>> GetMeetingsAsync(
        Guid? teamId, DateTime fromUtc, DateTime toUtc, TeamScope scope, CancellationToken ct);

    /// <summary>
    /// One MDT with its team's name and counts of the items the viewer may see; null if there's no such meeting or it
    /// isn't an MDT.
    /// </summary>
    Task<MdtMeetingRow?> GetMeetingRowAsync(Guid meetingId, TeamScope scope, CancellationToken ct);

    /// <summary>How many items on a meeting are still to be discussed, and how many have an outcome.</summary>
    Task<(int Open, int Discussed)> CountOnMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Adds a new item and its audit entry.</summary>
    Task AddAsync(MdtItem item, PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>
    /// Saves changes to an item loaded with <see cref="GetForUpdateAsync"/>, with its audit entry; if someone else
    /// saved it since (its row version), a 409 (<see cref="MdtRules.ChangedMeanwhileMessage"/>) and nothing is saved.
    /// </summary>
    Task SaveAsync(PatientPathwayAuditEntry audit, CancellationToken ct);

    /// <summary>Saves changes to an item that writes no audit entry (its presenter, MC21); a 409 as <see cref="SaveAsync(PatientPathwayAuditEntry, CancellationToken)"/>.</summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>The lab's work on these pathways (MC21): their samples not entered in error, oldest taken first, with their results.</summary>
    Task<IReadOnlyList<MdtLabWork>> GetLabWorkAsync(IReadOnlyCollection<Guid> pathwayIds, CancellationToken ct);

    /// <summary>
    /// Brings these items' attendee grants in line (<see cref="MdtGrants.Sync"/>), unsaved: the next save writes them.
    /// Each item's <see cref="MdtItem.Meeting"/> must be read with its attendees.
    /// </summary>
    Task SyncGrantsAsync(IReadOnlyCollection<MdtItem> items, DateTime nowUtc, CancellationToken ct);

    /// <summary>The same for every item on a meeting (tracked, its attendees as changed), unsaved: after a change of attendees.</summary>
    Task SyncMeetingGrantsAsync(Meeting meeting, DateTime nowUtc, CancellationToken ct);

    /// <summary>Ends the attendee grants that have lapsed (<see cref="MdtGrantsJob"/>), in one statement; returns how many.</summary>
    Task<int> EndLapsedGrantsAsync(DateTime nowUtc, CancellationToken ct);
}

/// <summary>A sample on an MDT item's pathway, with its result once reported (MC21).</summary>
public sealed record MdtLabWork(
    Guid PathwayId,
    Guid SpecimenId,
    string Number,
    string? TypeName,
    MedApp.Domain.Specimens.SpecimenStatus Status,
    DateTime? ReportedAtUtc,
    Guid? ResultId);
