using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

/// <summary>Assigns a staff member (admins only). Assignment puts the pathway on their task list.</summary>
public sealed record AssignPatientPathwayCommand(Guid PathwayId, Guid UserId, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
