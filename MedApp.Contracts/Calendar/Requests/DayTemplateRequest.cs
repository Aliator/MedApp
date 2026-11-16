namespace MedApp.Contracts.Calendar.Requests;

/// <summary>
/// A day template, to add or to replace one with (an edit applies from today; past days keep what they had).
/// </summary>
public sealed class DayTemplateRequest
{
    /// <summary>At most 100 characters, unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>At most 30, inside the working day, not overlapping each other.</summary>
    public List<DayTemplateBlockRequest> Blocks { get; set; } = [];
}

/// <summary>One block of a day template, at UK clock times.</summary>
public sealed class DayTemplateBlockRequest
{
    public Guid ActivityTypeId { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    /// <summary>Optional, at most 200 characters.</summary>
    public string? Note { get; set; }
}
