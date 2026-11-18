using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.UpdateClinicSession;

/// <summary>
/// Changes a Planned session's room, specialty, time or slot length, from the version the caller read
/// (<paramref name="ExpectedLastUpdated"/>, a 409 if it has changed since). With appointments booked into it only the room
/// and specialty may change (centre plan decision 127); a new room must be free, and its appointments move with it.
/// </summary>
public sealed record UpdateClinicSessionCommand(
    Guid SessionId,
    Guid RoomId,
    Guid SpecialtyId,
    DateTime StartUtc,
    DateTime EndUtc,
    int SlotMinutes,
    DateTime? ExpectedLastUpdated,
    CalendarActor Actor)
    : IRequest<ClinicSessionResponse>;
