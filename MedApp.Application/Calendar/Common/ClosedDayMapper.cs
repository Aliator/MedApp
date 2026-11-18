using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>Closed days as the API returns them.</summary>
public static class ClosedDayMapper
{
    /// <summary>
    /// A closed day; past (so it can't be removed) when it's before <paramref name="today"/> (UK).
    /// <paramref name="siteName"/> names its site (null for a day closed everywhere).
    /// </summary>
    public static ClosedDayResponse ToResponse(ClosedDay day, DateOnly today, string? siteName = null) => new()
    {
        Id = day.Id,
        Date = day.Date,
        Name = day.Name,
        SiteId = day.SiteId,
        SiteName = day.SiteId is null ? null : siteName,
        IsPast = day.Date < today
    };

    /// <summary>A bank holiday that wasn't added, and why.</summary>
    public static SkippedBankHolidayResponse ToSkipped(BankHoliday holiday, string reason) => new()
    {
        Date = holiday.Date,
        Name = holiday.Name,
        Reason = reason
    };
}
