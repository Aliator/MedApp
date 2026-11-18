using MedApp.Contracts.PatientMessages.Responses;
using MediatR;

namespace MedApp.Application.PatientMessages.Queries.GetPatientMessagingStatus;

/// <summary>Whether patients are texted and emailed here, and how.</summary>
public sealed record GetPatientMessagingStatusQuery : IRequest<PatientMessagingStatusResponse>;
