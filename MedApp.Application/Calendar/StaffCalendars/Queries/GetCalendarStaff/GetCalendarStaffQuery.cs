using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

/// <summary>Every member of staff, by username, with their default week template now: whose calendars can be shown.</summary>
public sealed record GetCalendarStaffQuery : IRequest<IReadOnlyList<StaffMemberResponse>>;
