namespace MedApp.Domain.Calendar;

/// <summary>
/// A day the clinic is closed, such as a bank holiday or a staff training day: a UK date and a name ("Christmas Day").
/// Admins keep the list. A closed day is like a weekend for everything new: nothing can be booked, blocked or have a
/// meeting on it, and templates don't apply on it. What was already on it stays, flagged. One per date.
/// </summary>
public sealed class ClosedDay
{
    public Guid Id { get; init; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; init; }

    /// <summary>What it is, e.g. "Christmas Day". About the clinic, never a patient.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>The admin who added it. No foreign key to the users table.</summary>
    public Guid CreatedByUserId { get; init; }
}
