using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

public static class ProcedureNoteMapper
{
    /// <summary>Shown as the author of a note moved from the old single notes field whose creator wasn't recorded.</summary>
    public const string UnknownAuthor = "Unknown";

    /// <summary>
    /// A note as the procedure page shows it. Only its author (<paramref name="viewerId"/>) can edit it, and only while
    /// <paramref name="canChange"/> (the procedure's notes can change, and its patient's record isn't restricted or
    /// anonymised).
    /// </summary>
    public static PatientProcedureNoteResponse ToResponse(
        PatientProcedureNote note,
        IReadOnlyDictionary<Guid, string> usernames,
        Guid? viewerId,
        bool canChange) => new()
    {
        Id = note.Id,
        AuthorUserId = note.AuthorUserId,
        AuthorUsername = AuthorName(note.AuthorUserId, usernames),
        Text = note.Text,
        CreatedAtUtc = note.CreatedAtUtc,
        LastEditedAtUtc = note.LastEditedAtUtc,
        Version = note.Version,
        CanEdit = canChange && note.AuthorUserId is not null && note.AuthorUserId == viewerId
    };

    /// <summary>A procedure's notes, newest first.</summary>
    public static List<PatientProcedureNoteResponse> ToResponses(
        IEnumerable<PatientProcedureNote> notes,
        IReadOnlyDictionary<Guid, string> usernames,
        Guid? viewerId,
        bool canChange) =>
        notes
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Select(n => ToResponse(n, usernames, viewerId, canChange))
            .ToList();

    /// <summary>
    /// The deprecated single <c>Notes</c> string of a procedure's response: its first note's text (the oldest, which is
    /// where the migration put the old field's text), or with no notes, the old field itself, for pages loaded before
    /// notes became a list.
    /// </summary>
    public static string FirstNoteText(PatientProcedure procedure) =>
        procedure.Notes
            .OrderBy(n => n.CreatedAtUtc)
            .ThenBy(n => n.Id)
            .FirstOrDefault()?.Text
        ?? procedure.LegacyNotes;

    /// <summary>A note's author by username: "Deleted user" once their account is gone, "Unknown" when not recorded.</summary>
    public static string AuthorName(Guid? authorUserId, IReadOnlyDictionary<Guid, string> usernames) =>
        authorUserId is { } id
            ? usernames.TryGetValue(id, out var name) ? name : ProcedureMapper.DeletedUser
            : UnknownAuthor;
}
