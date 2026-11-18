namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>Skips an optional stage while it's the current stage, with a reason.</summary>
public sealed class SkipPathwayStageRequest
{
    /// <summary>Why: NotNeeded, PatientDeclined or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why (at most 500 characters); required when the reason is Other.</summary>
    public string? Detail { get; set; }
}
