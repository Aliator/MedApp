using MedApp.Application.Calendar.Common;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;

/// <summary>Removes a block, not on a past day. From its owner's calendar, or anyone's for an admin.</summary>
public sealed record DeleteCalendarBlockCommand(Guid BlockId, CalendarActor Actor) : IRequest;
