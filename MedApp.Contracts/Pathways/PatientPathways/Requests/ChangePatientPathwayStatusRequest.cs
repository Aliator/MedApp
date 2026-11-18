namespace MedApp.Contracts.Pathways.PatientPathways.Requests;

/// <summary>A manual status change. The pathway's allowedStatusChanges lists what's possible.</summary>
public sealed class ChangePatientPathwayStatusRequest
{
    public string Status { get; set; } = string.Empty;

    /// <summary>Required when blocking or cancelling.</summary>
    public string? Reason { get; set; }
}
