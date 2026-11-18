using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

public sealed class AssignPatientPathwayHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AssignPatientPathwayCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(AssignPatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);

        if (!PathwayWorkflow.CanChangeAssignments(pathway))
            throw new ConflictException($"Staff can't be assigned to a pathway that's {PathwayWorkflow.Describe(pathway.Status)}.");

        var usernames = await identityReadService.GetUsernamesByIdAsync([request.UserId], cancellationToken);
        if (!usernames.TryGetValue(request.UserId, out var username))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (pathway.Assignments.Any(a => a.UserId == request.UserId))
            throw new ConflictException($"{username} is already assigned to this pathway.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.Assign(request.UserId, username);

        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        // Tell them, unless they assigned themselves (they know already).
        if (AssignmentNotifications.For(pathway, request.UserId, request.Actor, now) is { } notification)
            await notificationSender.SendAsync(notification, cancellationToken);

        // Then the admins watching, after the person it's about.
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerCanCorrectRecords: request.Actor.Has(Permission.CorrectRecords));
    }
}
