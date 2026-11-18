namespace MedApp.Domain.Specialties;

/// <summary>
/// A clinical service the centre offers, such as Dermatology: admins manage the list. Every pathway template belongs
/// to one, a pathway keeps a copy of its template's, and staff belong to any number (<see cref="StaffSpecialty"/>).
/// One that's in use (by a template, a pathway or a member of staff) is archived, never deleted. It's an
/// organisational label only: it decides nothing clinical and limits nobody's reading.
/// </summary>
public sealed class Specialty
{
    /// <summary>
    /// <c>General</c>, which every database starts with (seeded through <c>HasData</c>): the migration that added
    /// specialties put every existing template and pathway in it, and it's the database's default for both, so an
    /// API copy from before specialties can still create them during a rollout. Admins may rename or archive it.
    /// </summary>
    public static readonly Guid GeneralId = Guid.Parse("33333333-0000-0000-0000-000000000001");

    public const string GeneralName = "General";

    public const int MaxNameLength = 100;

    /// <summary>The waiting-time targets a new specialty starts with, in days, by priority (the owner's defaults).</summary>
    public const int DefaultTargetDaysUrgent = 14;
    public const int DefaultTargetDaysHigh = 28;
    public const int DefaultTargetDaysMedium = 126;
    public const int DefaultTargetDaysLow = 126;

    /// <summary>The range a target may take, in days.</summary>
    public const int MinTargetDays = 1;
    public const int MaxTargetDays = 365;

    public Guid Id { get; init; }

    /// <summary>Unique, ignoring case.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Archived specialties can't be chosen for templates or given to staff; what has one keeps it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// One more for each change (a rename, archiving or restoring). A change names the version it started from, so a
    /// page loaded before another admin's change is refused (409); it's also a concurrency token, so two saves at once
    /// can't both win.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// The centre's own waiting-time targets for this specialty, in days, one per priority: a referral's clock copies
    /// its priority's when it's registered, and re-reads it when its priority or specialty changes. Changing them
    /// affects only clocks that copy them afterwards.
    /// </summary>
    public int TargetDaysLow { get; set; } = DefaultTargetDaysLow;
    public int TargetDaysMedium { get; set; } = DefaultTargetDaysMedium;
    public int TargetDaysHigh { get; set; } = DefaultTargetDaysHigh;
    public int TargetDaysUrgent { get; set; } = DefaultTargetDaysUrgent;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}
