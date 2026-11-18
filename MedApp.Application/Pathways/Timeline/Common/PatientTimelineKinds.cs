using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;

namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// Which <see cref="PatientTimelineKind"/> an audit entry is, worked out the same way in memory (<see cref="Of"/>)
/// and in SQL (<see cref="WhereKindIn"/>). Most follow the action; a status change is a block when it goes to Blocked
/// or back from it (cancelling a blocked pathway is a status change like any other), and a details edit is a target date change when its changes include the target date. Both read the entry's
/// JSON details, which <see cref="PathwayAudit"/> writes compactly with camelCase names, so the markers below match
/// exactly what it writes. A value someone typed can't fake one: JSON escapes its quotes.
/// </summary>
public static class PatientTimelineKinds
{
    /// <summary>A status change to Blocked.</summary>
    public const string ToBlocked = "\"to\":\"Blocked\"";

    /// <summary>A status change from Blocked.</summary>
    public const string FromBlocked = "\"from\":\"Blocked\"";

    /// <summary>A status change to Cancelled: from Blocked, that's a cancellation, not an unblock.</summary>
    public const string ToCancelled = "\"to\":\"Cancelled\"";

    /// <summary>A details edit whose changes include the target date.</summary>
    public const string DueDateChange = "\"dueDate\":{";

    public static string Names => string.Join(", ", Enum.GetNames<PatientTimelineKind>());

    public static bool IsKind(string? value) => PathwayEnums.TryParse<PatientTimelineKind>(value, out _);

    /// <summary>The kinds named (ignoring names that aren't kinds, which the validator rejects first).</summary>
    public static IReadOnlySet<PatientTimelineKind> Parse(IEnumerable<string>? names) =>
        (names ?? [])
            .Select(n => PathwayEnums.TryParse<PatientTimelineKind>(n, out var kind) ? kind : (PatientTimelineKind?)null)
            .OfType<PatientTimelineKind>()
            .ToHashSet();

    /// <summary>The kind of one entry.</summary>
    public static PatientTimelineKind Of(PatientPathwayAuditEntry entry) => entry.Action switch
    {
        Created => PatientTimelineKind.Created,
        StageStarted or StageFinished or StageUnlocked => PatientTimelineKind.Stages,
        StageNotesUpdated or NoteAdded or NoteEdited or NoteDeleted => PatientTimelineKind.Notes,
        Assigned or Unassigned => PatientTimelineKind.Assignments,
        StatusChanged => IsBlockChange(entry.Details) ? PatientTimelineKind.Blocks : PatientTimelineKind.Status,
        DetailsUpdated => entry.Details.Contains(DueDateChange) ? PatientTimelineKind.DueDates : PatientTimelineKind.Details,
        Deleted => PatientTimelineKind.Deleted,
        // Downloads are files too, though the timeline never shows them (PatientTimelineSearch leaves them out).
        AttachmentAdded or AttachmentDeleted or AttachmentDownloaded => PatientTimelineKind.Files,
        >= LetterDrafted and <= LetterWithdrawn => PatientTimelineKind.Letters,
        _ => PatientTimelineKind.Details
    };

    /// <summary>
    /// Entries of any of these kinds, as LINQ that EF translates (the same rules as <see cref="Of"/>). No audit entry is
    /// of the <see cref="PatientTimelineKind.Appointments"/> kind, so it's ignored here.
    /// </summary>
    public static IQueryable<PatientPathwayAuditEntry> WhereKindIn(
        this IQueryable<PatientPathwayAuditEntry> entries,
        IReadOnlySet<PatientTimelineKind> kinds)
    {
        var created = kinds.Contains(PatientTimelineKind.Created);
        var stages = kinds.Contains(PatientTimelineKind.Stages);
        var notes = kinds.Contains(PatientTimelineKind.Notes);
        var assignments = kinds.Contains(PatientTimelineKind.Assignments);
        var blocks = kinds.Contains(PatientTimelineKind.Blocks);
        var status = kinds.Contains(PatientTimelineKind.Status);
        var dueDates = kinds.Contains(PatientTimelineKind.DueDates);
        var details = kinds.Contains(PatientTimelineKind.Details);
        var deleted = kinds.Contains(PatientTimelineKind.Deleted);
        var files = kinds.Contains(PatientTimelineKind.Files);
        var letters = kinds.Contains(PatientTimelineKind.Letters);

        return entries.Where(e =>
            (created && e.Action == Created)
            || (stages && (e.Action == StageStarted || e.Action == StageFinished || e.Action == StageUnlocked))
            || (notes && (e.Action == StageNotesUpdated || e.Action == NoteAdded || e.Action == NoteEdited || e.Action == NoteDeleted))
            || (assignments && (e.Action == Assigned || e.Action == Unassigned))
            || (blocks && e.Action == StatusChanged
                && (e.Details.Contains(ToBlocked) || (e.Details.Contains(FromBlocked) && !e.Details.Contains(ToCancelled))))
            || (status && e.Action == StatusChanged
                && !e.Details.Contains(ToBlocked) && !(e.Details.Contains(FromBlocked) && !e.Details.Contains(ToCancelled)))
            || (dueDates && e.Action == DetailsUpdated && e.Details.Contains(DueDateChange))
            || (details && e.Action == DetailsUpdated && !e.Details.Contains(DueDateChange))
            || (deleted && e.Action == Deleted)
            || (files && (e.Action == AttachmentAdded || e.Action == AttachmentDeleted || e.Action == AttachmentDownloaded))
            || (letters && e.Action >= LetterDrafted && e.Action <= LetterWithdrawn));
    }

    /// <summary>To Blocked, or back from it (a cancellation from Blocked isn't an unblock).</summary>
    private static bool IsBlockChange(string details) =>
        details.Contains(ToBlocked) || (details.Contains(FromBlocked) && !details.Contains(ToCancelled));
}
