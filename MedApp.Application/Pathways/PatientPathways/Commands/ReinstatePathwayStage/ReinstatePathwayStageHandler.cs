using MedApp.Application.Common.Teams;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;

public sealed class ReinstatePathwayStageHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<ReinstatePathwayStageCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(ReinstatePathwayStageCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, boundary, request.Actor.Teams, cancellationToken);
        var stage = pathway.GetStageOrThrow(request.StageId);

        if (!PathwayWorkflow.CanReinstateStage(pathway, stage))
            throw new ConflictException(stage.IsSkipped
                ? $"Stages can't be reinstated while the pathway is {PathwayWorkflow.Describe(pathway.Status)}."
                : $"The '{stage.Name}' stage wasn't skipped, so there's nothing to reinstate.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.ReinstateStage(stage, request.Reason.Trim());

        // If it's now the current stage and involves a visit, it's asked for, as when any stage becomes current.
        if (PathwayWorkflow.CurrentStage(pathway)?.Id == stage.Id && stage.VisitRequirement != VisitRequirement.None)
        {
            var appointments = await appointmentRepository.GetForStagesForUpdateAsync([stage.Id], cancellationToken);
            StageVisits.RequestForCurrentStage(change, appointments);
        }

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
