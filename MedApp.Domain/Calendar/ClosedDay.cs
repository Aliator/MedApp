namespace MedApp.Domain.Calendar;

/// <summary>
/// A day the clinic is closed, such as a bank holiday or a staff training day: a UK date and a name ("Christmas Day"),
/// everywhere or at one site. Admins keep the list. A closed day everywhere is like a weekend for everything new:
/// nothing can be booked, blocked or have a meeting on it, and templates don't apply on it. What was already on it
/// stays, flagged. A site's closed day closes that site's rooms and the hours of staff working there (as working hours
/// and clinic sessions name sites). One everywhere per date, and one per site per date.
/// </summary>
public sealed class ClosedDay
{
    public Guid Id { get; init; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; init; }

    /// <summary>What it is, e.g. "Christmas Day". About the clinic, never a patient.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The site it closes, or null for everywhere.</summary>
    public Guid? SiteId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>The admin who added it. No foreign key to the users table.</summary>
    public Guid CreatedByUserId { get; init; }
}
