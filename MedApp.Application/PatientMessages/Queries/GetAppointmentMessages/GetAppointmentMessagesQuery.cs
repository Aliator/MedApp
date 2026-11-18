using MedApp.Contracts.PatientMessages.Responses;
using MediatR;

namespace MedApp.Application.PatientMessages.Queries.GetAppointmentMessages;

/// <summary>The texts and emails about an appointment, newest first.</summary>
public sealed record GetAppointmentMessagesQuery(Guid AppointmentId) : IRequest<List<PatientMessageResponse>>;
