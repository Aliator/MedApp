using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

/// <summary>
/// Blocks off time on a member of staff's calendar for an activity: inside the working day, not on a past day, and
/// clash-free. Their own, or anyone's for an admin.
/// </summary>
public sealed record CreateCalendarBlockCommand(
    Guid StaffUserId,
    Guid ActivityTypeId,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Note,
    CalendarActor Actor)
    : IRequest<CalendarBlockResponse>;
