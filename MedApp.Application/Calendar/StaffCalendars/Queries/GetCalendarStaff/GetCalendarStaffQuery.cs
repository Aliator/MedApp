using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

/// <summary>
/// Every member of staff, by username, with their default week template now: whose calendars can be shown. With
/// <paramref name="Permission"/> (a permission's name, exactly as stored), only those whose roles grant it, such as who
/// can be assigned a pathway (<c>WorkPathways</c>); a name that isn't a permission is a 400.
/// </summary>
public sealed record GetCalendarStaffQuery(string? Permission = null) : IRequest<IReadOnlyList<StaffMemberResponse>>;
