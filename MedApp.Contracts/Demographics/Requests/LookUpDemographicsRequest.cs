namespace MedApp.Contracts.Demographics.Requests;

/// <summary>
/// Who to look up: an NHS number, with or without spaces. In the body rather than the URL, so request logs and traces
/// never record it.
/// </summary>
public sealed class LookUpDemographicsRequest
{
    public string? NhsNumber { get; set; }
}
