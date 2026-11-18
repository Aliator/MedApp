using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;

public sealed class UnassignPatientPathwayHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<UnassignPatientPathwayCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(UnassignPatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);

        if (!PathwayWorkflow.CanChangeAssignments(pathway))
            throw new ConflictException($"Staff can't be unassigned from a pathway that's {PathwayWorkflow.Describe(pathway.Status)}.");

        var assignment = pathway.Assignments.FirstOrDefault(a => a.UserId == request.UserId)
                         ?? throw new NotFoundException($"User '{request.UserId}' isn't assigned to this pathway.");

        var usernames = await identityReadService.GetUsernamesByIdAsync([request.UserId], cancellationToken);
        var username = usernames.GetValueOrDefault(request.UserId, PathwayMapper.DeletedUser);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.Unassign(assignment, username);

        await repository.SaveAsync(change.AuditEntries, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerIsAdmin: request.Actor.IsAdmin);
    }
}
