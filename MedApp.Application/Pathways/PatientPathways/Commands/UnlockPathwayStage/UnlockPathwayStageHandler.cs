using MedApp.Application.Common.Teams;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;

public sealed class UnlockPathwayStageHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<UnlockPathwayStageCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(UnlockPathwayStageCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        if (!PathwayWorkflow.CanUnlockStage(pathway, stage))
            throw new ConflictException(stage is { IsCompleted: true, IsLocked: true }
                ? $"Stages can't be unlocked while the pathway is {PathwayWorkflow.Describe(pathway.Status)}."
                : $"The '{stage.Name}' stage isn't finished and locked, so there's nothing to unlock.");

        change.UnlockStage(stage);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change.AuditEntries, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions, viewerScope: request.Actor.Teams);
    }
}
