namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Replaces a stage's notes. Allowed on the current stage, or on a finished stage an admin has unlocked.</summary>
public sealed class UpdatePathwayStageNotesRequest
{
    public string Notes { get; set; } = string.Empty;
}
