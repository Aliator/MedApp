using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;

/// <summary>
/// Removes one day's template block from a member of staff's calendar: it's left off that <paramref name="Date"/>
/// (an exception). The template itself is unchanged.
/// </summary>
public sealed record RemoveTemplateBlockForDayCommand(Guid StaffUserId, DateOnly Date, Guid TemplateBlockId, CalendarActor Actor)
    : IRequest;
