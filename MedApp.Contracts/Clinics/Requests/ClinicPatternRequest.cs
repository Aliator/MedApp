namespace MedApp.Contracts.Clinics.Requests;

/// <summary>
/// A clinic that repeats: a clinician, a room, a specialty, a day of the week, UK clock times, a slot length, every 1 to
/// 4 weeks, from a date and optionally until one. Sessions are made from it only when someone asks (Create sessions).
/// </summary>
public sealed class ClinicPatternRequest
{
    public Guid StaffUserId { get; set; }

    public Guid RoomId { get; set; }

    public Guid SpecialtyId { get; set; }

    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    public TimeOnly Starts { get; set; }

    public TimeOnly Ends { get; set; }

    public int SlotMinutes { get; set; }

    public int EveryWeeks { get; set; } = 1;

    public DateOnly FromDate { get; set; }

    public DateOnly? UntilDate { get; set; }

    /// <summary>For a change: the pattern's <c>lastUpdated</c> as the caller last read it (a 409 if it changed since).</summary>
    public DateTime? ExpectedLastUpdated { get; set; }
}
