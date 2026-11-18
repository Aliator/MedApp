namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Reinstates a skipped stage (holders of UnlockStages), so it runs again in its place.</summary>
public sealed class ReinstatePathwayStageRequest
{
    /// <summary>Why it's reinstated (required, at most 500 characters).</summary>
    public string Reason { get; set; } = string.Empty;
}
