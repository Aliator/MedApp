using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;

/// <summary>A status change made by hand: block, unblock, cancel, complete or archive (see <see cref="PathwayWorkflow"/>).</summary>
public sealed record ChangePatientPathwayStatusCommand(
    Guid Id,
    string Status,
    string? Reason,
    PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
