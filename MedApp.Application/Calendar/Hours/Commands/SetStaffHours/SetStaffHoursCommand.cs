using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Hours.Commands.SetStaffHours;

/// <summary>One day of someone's working hours: the day by name, UK clock times, and the site.</summary>
public sealed record StaffHoursDay(string DayOfWeek, TimeOnly Starts, TimeOnly Ends, Guid SiteId) : IHoursDay
{
    TimeOnly IHoursDay.From => Starts;
    TimeOnly IHoursDay.To => Ends;
}

/// <summary>
/// Sets a member of staff's working hours from today (centre plan decision 119); past days keep what they had. No
/// days puts them back on the default. Each day's hours must be inside its site's opening hours that day. Holders of
/// ManageClinics or ManageAnyCalendar only. What's already on their calendar stays where it is.
/// </summary>
public sealed record SetStaffHoursCommand(Guid StaffUserId, IReadOnlyList<StaffHoursDay>? Days, CalendarActor Actor)
    : IRequest<StaffHoursResponse>;
