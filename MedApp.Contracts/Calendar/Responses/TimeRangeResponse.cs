namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A stretch of time, in UTC.</summary>
public sealed class TimeRangeResponse
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}
