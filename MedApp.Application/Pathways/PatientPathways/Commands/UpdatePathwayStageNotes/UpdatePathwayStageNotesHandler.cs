using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;

public sealed class UpdatePathwayStageNotesHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePathwayStageNotesCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(UpdatePathwayStageNotesCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);

        if (PathwayWorkflow.WhyStageCantBeEdited(pathway, stage) is { } reason)
            throw new ConflictException(reason);

        change.UpdateStageNotes(stage, request.Notes);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change.AuditEntries, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerCanCorrectRecords: request.Actor.Has(Permission.CorrectRecords));
    }
}
