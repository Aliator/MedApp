namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Replaces the text of one of your own notes on a procedure.</summary>
public sealed class EditProcedureNoteRequest
{
    /// <summary>The note, as Markdown (at most 4,000 characters).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The note's version the editor saw; if it has changed since, the edit is refused (409).</summary>
    public int ExpectedVersion { get; set; }
}
