namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Blocks off time on a calendar for an activity, inside the working day.</summary>
public sealed class CreateCalendarBlockRequest
{
    public Guid StaffUserId { get; set; }

    public Guid ActivityTypeId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>Optional, at most 200 characters. About the member of staff, never a patient.</summary>
    public string? Note { get; set; }
}
