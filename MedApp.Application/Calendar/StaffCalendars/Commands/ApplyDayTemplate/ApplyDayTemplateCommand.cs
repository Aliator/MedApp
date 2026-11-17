using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

/// <summary>
/// Gives one weekday of a member of staff's calendar one of their own day templates (or none, an empty day) instead of
/// the week template's. Blocks added by hand that day stay. Not on a past day.
/// </summary>
public sealed record ApplyDayTemplateCommand(Guid StaffUserId, DateOnly Date, Guid? DayTemplateId, CalendarActor Actor)
    : IRequest<CalendarDayResponse>;
