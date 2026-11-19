using MedApp.Application.Common.Identity;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

public static class PathwayNoteMapper
{
    /// <summary>Shown as the author of a note moved from the old single notes field whose creator wasn't recorded.</summary>
    public const string UnknownAuthor = "Unknown";

    /// <summary>
    /// A note as the pathway page shows it. Only its author (<paramref name="viewerId"/>) can edit it, and only while
    /// <paramref name="canChange"/> (the pathway's notes can change, and its patient's record isn't restricted or
    /// anonymised). It can be deleted, or its deletion asked for, in any status, while <paramref name="recordCanChange"/>
    /// (the patient's record isn't restricted or anonymised; <paramref name="canChange"/> when not given): deleted only by
    /// a holder of <see cref="Permission.CorrectRecords"/>, and asked for by anyone else, so the flags say what this viewer
    /// can do. Editing a note and asking for its deletion also need <see cref="Permission.WorkPathways"/>, as their
    /// endpoints do. <paramref name="viewerPermissions"/> are the viewer's, from their session; null means no viewer is
    /// known: the flags then say only what the note and record allow, with nothing that needs
    /// <see cref="Permission.CorrectRecords"/>. Editing a note and asking for its deletion also need the viewer's team to
    /// own the pathway (<paramref name="viewerTeamOwns"/>, the team walls): admins and the lab read others' notes.
    /// </summary>
    public static PatientPathwayNoteResponse ToResponse(
        PatientPathwayNote note,
        IReadOnlyDictionary<Guid, string> usernames,
        Guid? viewerId,
        bool canChange,
        bool? recordCanChange = null,
        IReadOnlySet<Permission>? viewerPermissions = null,
        bool viewerTeamOwns = true)
    {
        var canCorrectRecords = viewerPermissions?.Contains(Permission.CorrectRecords) == true;
        var canWork = PathwayMapper.Allows(viewerPermissions, Permission.WorkPathways) && viewerTeamOwns;
        return new PatientPathwayNoteResponse
        {
            Id = note.Id,
            AuthorUserId = note.AuthorUserId,
            AuthorUsername = AuthorName(note.AuthorUserId, usernames),
            Text = note.Text,
            CreatedAtUtc = note.CreatedAtUtc,
            LastEditedAtUtc = note.LastEditedAtUtc,
            Version = note.Version,
            CanEdit = canWork && canChange && !note.IsDeleted && note.AuthorUserId is not null && note.AuthorUserId == viewerId,
            CanDelete = canCorrectRecords && (recordCanChange ?? canChange) && !note.IsDeleted,
            CanRequestDeletion = !canCorrectRecords && canWork && (recordCanChange ?? canChange) && !note.IsDeleted
                && note.PendingDeletionRequestId is null,
            DeletionRequestId = note.PendingDeletionRequestId
        };
    }

    /// <summary>A pathway's notes, newest first, leaving out those an admin has deleted (they're kept, but hidden).</summary>
    public static List<PatientPathwayNoteResponse> ToResponses(
        IEnumerable<PatientPathwayNote> notes,
        IReadOnlyDictionary<Guid, string> usernames,
        Guid? viewerId,
        bool canChange,
        bool? recordCanChange = null,
        IReadOnlySet<Permission>? viewerPermissions = null,
        bool viewerTeamOwns = true) =>
        notes
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => ToResponse(n, usernames, viewerId, canChange, recordCanChange, viewerPermissions, viewerTeamOwns))
            .ToList();

    /// <summary>
    /// The deprecated single <c>Notes</c> string of a pathway's response: its first note's text (the oldest, which is
    /// where the migration put the old field's text), or with no notes, the old field itself, for pages loaded before
    /// notes became a list. Deleted notes are left out; once a pathway has notes, the old field is never shown again,
    /// so deleting the note the migration copied it into doesn't bring its text back.
    /// </summary>
    public static string FirstNoteText(PatientPathway pathway) =>
        pathway.Notes.Count == 0
            ? pathway.LegacyNotes
            : pathway.Notes
                  .Where(n => !n.IsDeleted)
                  .OrderBy(n => n.CreatedAtUtc)
                  .ThenBy(n => n.Id)
                  .FirstOrDefault()?.Text
              ?? string.Empty;

    /// <summary>A note's author by username: "Deleted user" once their account is gone, "Unknown" when not recorded.</summary>
    public static string AuthorName(Guid? authorUserId, IReadOnlyDictionary<Guid, string> usernames) =>
        authorUserId is { } id
            ? usernames.TryGetValue(id, out var name) ? name : PathwayMapper.DeletedUser
            : UnknownAuthor;
}
