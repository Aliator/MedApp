using System.Text.Json;
using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// The rules and messages of a procedure's notes, beside <see cref="ProcedureWorkflow.WhyNotesCantChange"/>: only a
/// note's author can edit it (admins can't edit anyone else's: what someone wrote in the clinical record stays theirs),
/// and an edit names the version it started from, so a stale one is refused.
/// <para>
/// Only admins delete notes, with a reason, and deleting is soft (the note is hidden, but kept). Staff ask an admin to
/// (a <see cref="ProcedureNoteDeletionRequest"/>), one request per note at a time. Both are allowed in any status of the
/// procedure, as a correction (a note on the wrong patient), like an admin deleting a file; neither while the patient's
/// record is restricted or anonymised.
/// </para>
/// </summary>
public static class ProcedureNotes
{
    public const string OnlyTheAuthorMessage = "Only the person who wrote a note can edit it.";

    public const string ChangedMeanwhileMessage =
        "This note was changed somewhere else (another tab or window) after you opened it. Reload to see it as it is now, then make your change again.";

    public const string DeletedMessage = "This note has been deleted.";

    public const string DeletedMeanwhileMessage =
        "This note, or the request to delete it, was changed somewhere else (another admin, tab or window) after you opened it. Reload to see it as it is now.";

    /// <summary>Asking to delete a note that someone else changed, asked to delete or deleted at the same moment.</summary>
    public const string RequestedMeanwhileMessage =
        "Someone else changed this note, or asked for it to be deleted, at the same moment. Reload to see it as it is now.";

    /// <summary>Asking to delete a note that already has a request waiting: who asked, and when.</summary>
    public static string AlreadyRequestedMessage(ProcedureNoteDeletionRequest? pending) =>
        pending is null
            ? "Someone has already asked for this note to be deleted. An admin will decide."
            : $"{pending.RequestedByUsername} already asked for this note to be deleted, {UkTime.Describe(pending.RequestedAtUtc)}. An admin will decide.";

    /// <summary>Deciding a request that's already been decided: by whom, when and how.</summary>
    public static string AlreadyDecidedMessage(ProcedureNoteDeletionRequest request)
    {
        var when = request.DecidedAtUtc is { } at ? $", {UkTime.Describe(at)}" : string.Empty;
        var by = string.IsNullOrEmpty(request.DecidedByUsername) ? "Someone" : request.DecidedByUsername;
        return request.Status switch
        {
            NoteDeletionRequestStatus.Approved => $"{by} already deleted this note{when}.",
            NoteDeletionRequestStatus.Declined => $"{by} already decided to keep this note{when}.",
            _ => "This request was closed without a decision: the note's procedure was deleted, or the patient's record anonymised."
        };
    }

    /// <summary>Why a note is being deleted, or should be: not blank, and at most 500 characters.</summary>
    public static IRuleBuilderOptions<T, string> DeletionReason<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Say why the note should be deleted.")
            .MaximumLength(PatientProcedureNote.MaxDeletionReasonLength)
            .WithMessage($"A reason must not exceed {PatientProcedureNote.MaxDeletionReasonLength} characters.");

    /// <summary>The note's text: not blank, and at most <see cref="PatientProcedureNote.MaxTextLength"/> characters.</summary>
    public static IRuleBuilderOptions<T, string> NoteText<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Write something in the note.")
            .MaximumLength(PatientProcedureNote.MaxTextLength)
            .WithMessage($"A note must not exceed {PatientProcedureNote.MaxTextLength} characters.");

    /// <summary>
    /// The note's ID and version a NoteAdded, NoteEdited or NoteDeleted audit entry names (for a deletion, the version
    /// that was deleted), or null for any other entry.
    /// </summary>
    public static NoteVersionKey? VersionOf(PatientProcedureAuditEntry entry)
    {
        if (entry.Action is not (PatientProcedureAuditAction.NoteAdded or PatientProcedureAuditAction.NoteEdited
            or PatientProcedureAuditAction.NoteDeleted))
            return null;

        try
        {
            using var details = JsonDocument.Parse(entry.Details);
            var root = details.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("noteId", out var noteId) && noteId.ValueKind == JsonValueKind.String
                   && noteId.TryGetGuid(out var id)
                   && root.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.Number
                   && version.TryGetInt32(out var number)
                ? new NoteVersionKey(id, number)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>One version of one note.</summary>
public sealed record NoteVersionKey(Guid NoteId, int Version);
