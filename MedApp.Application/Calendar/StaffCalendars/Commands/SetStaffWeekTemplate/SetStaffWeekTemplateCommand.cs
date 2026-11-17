using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;

/// <summary>
/// Sets a member of staff's default week template (one of their own, or none) from today; past days keep the one they
/// had. Their own, or anyone's for an admin.
/// </summary>
public sealed record SetStaffWeekTemplateCommand(Guid StaffUserId, Guid? WeekTemplateId, CalendarActor Actor)
    : IRequest<StaffWeekTemplateResponse>;
