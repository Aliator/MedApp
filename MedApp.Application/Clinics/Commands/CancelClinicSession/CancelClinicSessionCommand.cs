using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CancelClinicSession;

/// <summary>
/// Cancels a Planned session with a reason, from the version the caller read (<paramref name="ExpectedLastUpdated"/>), and
/// every appointment Booked into it with that reason, in one save (centre plan decision 126). Returns the patients to
/// tell. Not on a past day. With <paramref name="TellPatients"/>, each of them is texted or emailed
/// (<c>AppointmentCancelled</c>), as they chose, when patient messages are on.
/// </summary>
public sealed record CancelClinicSessionCommand(
    Guid SessionId,
    string Reason,
    DateTime? ExpectedLastUpdated,
    CalendarActor Actor,
    bool TellPatients = false)
    : IRequest<CancelClinicSessionResponse>;
