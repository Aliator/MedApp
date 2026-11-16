namespace MedApp.Contracts.Demographics.Responses;

/// <summary>Whether the patient forms can look someone up by NHS number, and in what.</summary>
public sealed class DemographicsStatusResponse
{
    public bool IsAvailable { get; set; }

    /// <summary>What the lookup uses, in words; null when there's none.</summary>
    public string? Source { get; set; }
}
