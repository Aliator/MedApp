using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.Hours.Queries.GetStaffHours;

/// <summary>A member of staff's working hours today (anyone signed in: their calendar shows them anyway).</summary>
public sealed record GetStaffHoursQuery(Guid StaffUserId, CalendarActor Actor) : IRequest<StaffHoursResponse>;
