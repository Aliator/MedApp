using MedApp.Contracts.Calendar.Responses;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Calendar;
using MedApp.Domain.Sites;

namespace MedApp.Application.Calendar.Common;

/// <summary>Staff working hours and site opening hours as the API returns them, Monday first.</summary>
public static class WorkingHoursMapper
{
    /// <summary>
    /// A person's hours from their rows valid today (none: the default's days, at no particular site), naming each
    /// day's site from <paramref name="siteNames"/>.
    /// </summary>
    public static StaffHoursResponse ToResponse(
        Guid staffUserId, IReadOnlyCollection<StaffHours> rows, IReadOnlyDictionary<Guid, string> siteNames, bool canEdit) =>
        new()
        {
            StaffUserId = staffUserId,
            HasOwnHours = rows.Count > 0,
            Since = rows.Count > 0 ? rows.Max(r => r.ValidFrom) : null,
            CanEdit = canEdit,
            Days = rows.Count > 0
                ? rows.OrderBy(r => Order(r.DayOfWeek))
                    .Select(r => new StaffHoursDayResponse
                    {
                        DayOfWeek = r.DayOfWeek.ToString(),
                        Starts = r.Starts,
                        Ends = r.Ends,
                        SiteId = r.SiteId,
                        SiteName = siteNames.GetValueOrDefault(r.SiteId)
                    })
                    .ToList()
                : DefaultDays()
                    .Select(d => new StaffHoursDayResponse
                    {
                        DayOfWeek = d.ToString(), Starts = WorkingHours.Start, Ends = WorkingHours.End
                    })
                    .ToList()
        };

    /// <summary>A site's hours from its rows valid today (none: the default's days).</summary>
    public static SiteHoursResponse ToResponse(Guid siteId, IReadOnlyCollection<SiteHours> rows) =>
        new()
        {
            SiteId = siteId,
            HasOwnHours = rows.Count > 0,
            Since = rows.Count > 0 ? rows.Max(r => r.ValidFrom) : null,
            Days = rows.Count > 0
                ? rows.OrderBy(r => Order(r.DayOfWeek))
                    .Select(r => new SiteHoursDayResponse { DayOfWeek = r.DayOfWeek.ToString(), Opens = r.Opens, Closes = r.Closes })
                    .ToList()
                : DefaultDays()
                    .Select(d => new SiteHoursDayResponse { DayOfWeek = d.ToString(), Opens = WorkingHours.Start, Closes = WorkingHours.End })
                    .ToList()
        };

    /// <summary>Monday 0 to Sunday 6.</summary>
    public static int Order(DayOfWeek day) => ((int)day + 6) % 7;

    private static IEnumerable<DayOfWeek> DefaultDays() =>
        Enum.GetValues<DayOfWeek>().Where(WorkingHours.IsDefaultDay).OrderBy(Order);
}
