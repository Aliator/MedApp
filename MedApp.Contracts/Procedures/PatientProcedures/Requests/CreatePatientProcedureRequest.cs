namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Starts a procedure for a patient from a template; the template's stages are copied onto it.</summary>
public sealed class CreatePatientProcedureRequest
{
    public Guid PatientId { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Optional; defaults to the template's name.</summary>
    public string? Title { get; set; }

    public string Notes { get; set; } = string.Empty;

    /// <summary>Due by the end of this day, UK time.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Low, Medium, High or Urgent.</summary>
    public string Priority { get; set; } = string.Empty;
}
