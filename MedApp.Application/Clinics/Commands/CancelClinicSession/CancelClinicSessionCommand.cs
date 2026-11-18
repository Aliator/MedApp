using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CancelClinicSession;

/// <summary>
/// Cancels a Planned session with a reason, from the version the caller read (<paramref name="ExpectedLastUpdated"/>), and
/// every appointment Booked into it with that reason, in one save (centre plan decision 126). Returns the patients to
/// tell. Not on a past day.
/// </summary>
public sealed record CancelClinicSessionCommand(
    Guid SessionId,
    string Reason,
    DateTime? ExpectedLastUpdated,
    CalendarActor Actor)
    : IRequest<CancelClinicSessionResponse>;
