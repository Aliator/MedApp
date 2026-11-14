namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

/// <summary>Replaces a stage's notes. Allowed on the current stage, or on a finished stage an admin has unlocked.</summary>
public sealed class UpdateProcedureStageNotesRequest
{
    public string Notes { get; set; } = string.Empty;
}
