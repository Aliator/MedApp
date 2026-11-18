using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Queries.GetClinicSessionById;

/// <summary>
/// A clinic session with its slots and who is booked into each. It names the patients, so reading it records
/// <c>PatientListed</c> for them, as the calendar does.
/// </summary>
public sealed record GetClinicSessionByIdQuery(Guid SessionId, CalendarActor Actor) : IRequest<ClinicSessionDetailResponse>;
