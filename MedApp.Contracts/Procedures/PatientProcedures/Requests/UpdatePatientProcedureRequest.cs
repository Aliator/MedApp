namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>
/// Edits a procedure's details. Only the fields that are set change; stages come from the template and don't, and notes
/// are added and edited one at a time (<c>.../notes</c>).
/// </summary>
public sealed class UpdatePatientProcedureRequest
{
    public string? Title { get; set; }

    /// <summary>Due by the end of this day, UK time.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string? Priority { get; set; }
}
