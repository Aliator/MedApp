using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;

/// <summary>
/// Changes a block's activity, time and note: not on (or to) a past day, inside the working day, clash-free. On its
/// owner's calendar, or anyone's for an admin.
/// </summary>
public sealed record UpdateCalendarBlockCommand(
    Guid BlockId,
    Guid ActivityTypeId,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Note,
    CalendarActor Actor)
    : IRequest<CalendarBlockResponse>;
