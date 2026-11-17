namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Adds a note to a procedure, by the signed-in user.</summary>
public sealed class AddProcedureNoteRequest
{
    /// <summary>The note, as Markdown (at most 4,000 characters).</summary>
    public string Text { get; set; } = string.Empty;
}
