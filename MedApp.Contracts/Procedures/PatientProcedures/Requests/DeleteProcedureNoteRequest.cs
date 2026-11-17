namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Deletes a note on a procedure (admins): it's hidden from the procedure page, but kept, with the reason.</summary>
public sealed class DeleteProcedureNoteRequest
{
    /// <summary>Why it's being deleted (required, at most 500 characters).</summary>
    public string Reason { get; set; } = string.Empty;
}
