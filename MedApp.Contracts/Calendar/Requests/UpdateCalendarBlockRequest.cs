namespace MedApp.Contracts.Calendar.Requests;

/// <summary>
/// Changes a block: its activity, time and note. Also changes one day's template block, which then becomes a block of
/// its own on that day.
/// </summary>
public sealed class UpdateCalendarBlockRequest
{
    public Guid ActivityTypeId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>Optional, at most 200 characters. About the member of staff, never a patient.</summary>
    public string? Note { get; set; }
}
