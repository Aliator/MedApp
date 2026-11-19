using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;

/// <summary>
/// Picks up a stage handed to the actor's team (MC7, the relay): it leaves the team's New for us view. Only the team that
/// works it, while it's current; again changes nothing.
/// </summary>
public sealed record PickUpPathwayStageCommand(Guid PathwayId, Guid StageId, PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
