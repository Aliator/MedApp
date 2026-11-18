using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetSiteHours;

/// <summary>One day of a site's opening hours: the day by name and UK clock times.</summary>
public sealed record SiteHoursDay(string DayOfWeek, TimeOnly Opens, TimeOnly Closes) : IHoursDay
{
    TimeOnly IHoursDay.From => Opens;
    TimeOnly IHoursDay.To => Closes;
}

/// <summary>
/// Sets a site's opening hours from today (centre plan decisions 119 and 122); past days keep what they had. No days
/// puts it back on the default. Refused while anyone's working hours there would fall outside them.
/// </summary>
public sealed record SetSiteHoursCommand(Guid SiteId, IReadOnlyList<SiteHoursDay>? Days) : IRequest<SiteHoursResponse>;
