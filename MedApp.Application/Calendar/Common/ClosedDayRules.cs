using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>What can change about closed days: past days don't change, as everywhere on a calendar.</summary>
public static class ClosedDayRules
{
    public const string PastCantBeAdded = "Past days don't change: a closed day can't be added before today.";
    public const string PastCantBeRemoved = "Past days don't change: a closed day before today can't be removed.";

    /// <summary>Why a date that's already closed can't be added again.</summary>
    public static string AlreadyClosed(ClosedDay existing) =>
        $"{UkTime.DescribeDate(existing.Date)} is already a closed day: {existing.Name}.";

    /// <summary>A 409 for a date before today.</summary>
    public static void EnsureNotPast(DateOnly date, DateOnly today, string message)
    {
        if (date < today)
            throw new ConflictException(message);
    }
}
