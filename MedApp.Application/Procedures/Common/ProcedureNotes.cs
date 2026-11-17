using System.Text.Json;
using FluentValidation;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// The rules and messages of a procedure's notes, beside <see cref="ProcedureWorkflow.WhyNotesCantChange"/>: only a
/// note's author can edit it (admins can't edit anyone else's: what someone wrote in the clinical record stays theirs),
/// nobody can delete one, and an edit names the version it started from, so a stale one is refused.
/// </summary>
public static class ProcedureNotes
{
    public const string OnlyTheAuthorMessage = "Only the person who wrote a note can edit it.";

    public const string ChangedMeanwhileMessage =
        "This note was changed somewhere else (another tab or window) after you opened it. Reload to see it as it is now, then make your change again.";

    /// <summary>The note's text: not blank, and at most <see cref="PatientProcedureNote.MaxTextLength"/> characters.</summary>
    public static IRuleBuilderOptions<T, string> NoteText<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Write something in the note.")
            .MaximumLength(PatientProcedureNote.MaxTextLength)
            .WithMessage($"A note must not exceed {PatientProcedureNote.MaxTextLength} characters.");

    /// <summary>The note's ID and version a NoteAdded or NoteEdited audit entry names, or null for any other entry.</summary>
    public static NoteVersionKey? VersionOf(PatientProcedureAuditEntry entry)
    {
        if (entry.Action is not (PatientProcedureAuditAction.NoteAdded or PatientProcedureAuditAction.NoteEdited))
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
