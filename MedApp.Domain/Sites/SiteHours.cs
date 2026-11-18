using MedApp.Domain.Calendar;

namespace MedApp.Domain.Sites;

/// <summary>
/// When a site is open on one day of the week, over a range of dates (see <see cref="IDatedRow"/>). A site with no rows
/// valid on a date keeps the default opening hours (Monday to Friday, 08:00 to 18:00); one with rows is open only on the
/// days they name. Staff working there work inside them (centre plan decisions 119 and 122: an evening clinic is just
/// a site open until 20:00 on a Tuesday).
/// </summary>
public sealed class SiteHours : IDatedRow
{
    public Guid Id { get; init; }

    public Guid SiteId { get; init; }

    /// <summary>Any day of the week (stored as .NET's number: Sunday 0 to Saturday 6).</summary>
    public DayOfWeek DayOfWeek { get; init; }

    /// <summary>UK clock time, between 06:00 and 23:00, on a 5-minute boundary.</summary>
    public TimeOnly Opens { get; init; }

    /// <summary>UK clock time, after <see cref="Opens"/>, at most 23:00.</summary>
    public TimeOnly Closes { get; init; }

    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; set; }
}
