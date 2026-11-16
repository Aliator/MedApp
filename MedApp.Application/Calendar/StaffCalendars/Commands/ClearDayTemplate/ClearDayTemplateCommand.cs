using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;

/// <summary>Takes a day of a member of staff's calendar back to its week template's day template. Not on a past day.</summary>
public sealed record ClearDayTemplateCommand(Guid StaffUserId, DateOnly Date, CalendarActor Actor) : IRequest;
