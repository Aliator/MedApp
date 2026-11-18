using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CreateClinicSession;

/// <summary>
/// A new clinic session (centre plan decision 123): in the future, inside the clinician's working day and the room's
/// site's opening hours, clear of everything on their calendar and in the room, cut into whole slots.
/// </summary>
public sealed record CreateClinicSessionCommand(
    Guid StaffUserId,
    Guid RoomId,
    Guid TeamId,
    DateTime StartUtc,
    DateTime EndUtc,
    int SlotMinutes,
    CalendarActor Actor)
    : IRequest<ClinicSessionResponse>;
