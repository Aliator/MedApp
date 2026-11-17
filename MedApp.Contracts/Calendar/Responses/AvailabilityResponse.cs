namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// When everyone asked about is free, each working day of a range (after now, inside the working day, on 5-minute
/// boundaries), suggested start times for a meeting of the length asked, and each person's busy times.
/// </summary>
public sealed class AvailabilityResponse
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>Each working day of the range, with the times everyone is free.</summary>
    public List<AvailabilityDayResponse> Days { get; set; } = [];

    /// <summary>
    /// Up to 10 times everyone is free for the whole length, earliest first, on 15-minute steps, at most 3 a day and
    /// not overlapping each other.
    /// </summary>
    public List<TimeRangeResponse> Suggestions { get; set; } = [];

    /// <summary>What each person has on in the range (appointments never name the patient), by start time.</summary>
    public List<BusyTimeResponse> Busy { get; set; } = [];
}

public sealed class AvailabilityDayResponse
{
    public DateOnly Date { get; set; }

    public List<TimeRangeResponse> Free { get; set; } = [];
}

/// <summary>Something on one person's calendar, for saying who's busy at a time.</summary>
public sealed class BusyTimeResponse
{
    public Guid StaffUserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>What it is, as a clash names it: "Lunch", "another appointment", "the meeting 'MDT'".</summary>
    public string What { get; set; } = string.Empty;
}
