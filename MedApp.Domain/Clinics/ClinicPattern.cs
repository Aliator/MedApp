using MedApp.Domain.Sites;
using MedApp.Domain.Teams;

namespace MedApp.Domain.Clinics;

/// <summary>
/// A clinic that repeats ("k.ashford's Dermatology clinic in Room 2, Tuesdays 09:00 to 12:30, 20-minute slots, every
/// week"), from which a holder of ManageClinics makes <see cref="ClinicSession"/>s for the weeks ahead when they ask
/// (centre plan decision 125). Nothing makes sessions in the background. It ends with <see cref="UntilDate"/>.
/// </summary>
public sealed class ClinicPattern
{
    public Guid Id { get; init; }

    public Guid StaffUserId { get; set; }

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    /// <summary>Any day of the week (stored as .NET's number: Sunday 0 to Saturday 6).</summary>
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>UK clock time, between 06:00 and 23:00, on a 5-minute boundary.</summary>
    public TimeOnly Starts { get; set; }

    public TimeOnly Ends { get; set; }

    public int SlotMinutes { get; set; }

    /// <summary>Every 1 to 4 weeks, counted from the first <see cref="DayOfWeek"/> on or after <see cref="FromDate"/>.</summary>
    public int EveryWeeks { get; set; }

    public DateOnly FromDate { get; set; }

    /// <summary>The last date it makes a session on, or null for no end.</summary>
    public DateOnly? UntilDate { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid CreatedByUserId { get; init; }

    public DateTime LastUpdated { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
