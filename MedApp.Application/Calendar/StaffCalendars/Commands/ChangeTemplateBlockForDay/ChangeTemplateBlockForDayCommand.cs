using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;

/// <summary>
/// Changes one day's template block on a member of staff's calendar: the template block is left off that
/// <paramref name="Date"/> (an exception), and a block of its own with the new values takes its place, on the same
/// day, clash-free. The template itself is unchanged.
/// </summary>
public sealed record ChangeTemplateBlockForDayCommand(
    Guid StaffUserId,
    DateOnly Date,
    Guid TemplateBlockId,
    Guid ActivityTypeId,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Note,
    CalendarActor Actor)
    : IRequest<CalendarBlockResponse>;
