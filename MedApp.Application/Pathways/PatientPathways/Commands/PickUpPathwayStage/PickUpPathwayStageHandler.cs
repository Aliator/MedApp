using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.PickUpPathwayStage;

/// <summary>
/// Picks up a handed-over stage (MC7): the team that works it (its owner, else the pathway's own), while it's current
/// and the pathway can be worked. Audited (StagePickedUp) once; picking it up again saves nothing.
/// </summary>
public sealed class PickUpPathwayStageHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<PickUpPathwayStageCommand, PatientPathwayResponse>
{
    public const string NotCurrentMessage = "Only a current stage can be picked up.";
    public const string NotHandedOverMessage = "This stage wasn’t handed over, so there’s nothing to pick up.";

    public async Task<PatientPathwayResponse> Handle(PickUpPathwayStageCommand request, CancellationToken cancellationToken)
    {
        var (pathway, stage) = await repository.GetForStageWorkOrThrowAsync(
            request.PathwayId, request.StageId, boundary, request.Actor.Teams, cancellationToken);

        if (!PathwayWorkflow.CanWorkOnStages(pathway))
            throw new ConflictException(PathwayBranches.OnHoldOrClosedMessage);
        if (!PathwayWorkflow.IsCurrent(pathway, stage))
            throw new ConflictException(NotCurrentMessage);
        if (stage.HandedOverFromTeamId is null)
            throw new ConflictException(NotHandedOverMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        if (change.PickUp(stage))
        {
            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(
            pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId,
            viewerPermissions: request.Actor.Permissions, viewerScope: request.Actor.Teams);
    }
}
