using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// What can change about closed days: past days don't change, as everywhere on a calendar, and a place is closed at
/// most once a day.
/// </summary>
public static class ClosedDayRules
{
    public const string PastCantBeAdded = "Past days don't change: a closed day can't be added before today.";
    public const string PastCantBeRemoved = "Past days don't change: a closed day before today can't be removed.";

    /// <summary>Why a date that's already closed everywhere can't be closed again, everywhere or at a site.</summary>
    public static string AlreadyClosed(ClosedDay existing) =>
        $"{UkTime.DescribeDate(existing.Date)} is already a closed day: {existing.Name}.";

    /// <summary>Why a site that's already closed on a date can't be closed again.</summary>
    public static string AlreadyClosedAt(ClosedDay existing, string siteName) =>
        $"{UkTime.DescribeDate(existing.Date)} is already a closed day at {siteName}: {existing.Name}.";

    /// <summary>
    /// A 409 if the place is already closed on the date: a day closed everywhere stops everything; a site's closed day
    /// stops another at that site. A day closed everywhere can be added over sites' closed days.
    /// </summary>
    public static void EnsureNotClosed(IEnumerable<ClosedDay> onDate, Guid? siteId, string? siteName)
    {
        var days = onDate.ToList();
        if (days.FirstOrDefault(d => d.SiteId is null) is { } everywhere)
            throw new ConflictException(AlreadyClosed(everywhere));

        if (siteId is not null && days.FirstOrDefault(d => d.SiteId == siteId) is { } atSite)
            throw new ConflictException(AlreadyClosedAt(atSite, siteName ?? string.Empty));
    }

    /// <summary>A 409 for a date before today.</summary>
    public static void EnsureNotPast(DateOnly date, DateOnly today, string message)
    {
        if (date < today)
            throw new ConflictException(message);
    }
}
