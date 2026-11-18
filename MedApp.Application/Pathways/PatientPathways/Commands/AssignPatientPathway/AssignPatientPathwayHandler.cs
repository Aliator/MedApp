using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;

public sealed class AssignPatientPathwayHandler(
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPermissionHolders permissionHolders,
    INotificationSender notificationSender,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AssignPatientPathwayCommand, PatientPathwayResponse>
{
    /// <summary>
    /// The message when the person chosen doesn't hold <see cref="Permission.WorkPathways"/>: only those who can work
    /// on a pathway can be given one (decision 68).
    /// </summary>
    public static string CantWorkOnPathways(string username) =>
        $"{username} can't be assigned: their roles don't allow working on pathways.";

    public async Task<PatientPathwayResponse> Handle(AssignPatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.PathwayId, cancellationToken);

        if (!PathwayWorkflow.CanChangeAssignments(pathway))
            throw new ConflictException($"Staff can't be assigned to a pathway that's {PathwayWorkflow.Describe(pathway.Status)}.");

        var usernames = await identityReadService.GetUsernamesByIdAsync([request.UserId], cancellationToken);
        if (!usernames.TryGetValue(request.UserId, out var username))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        // Only people who can work on pathways are given one (roles are read now, so a role taken away counts at once).
        var holders = await permissionHolders.WhoHoldsAsync([request.UserId], Permission.WorkPathways, cancellationToken);
        if (!holders.Contains(request.UserId))
            throw new ValidationException(
                [new ValidationFailure(nameof(AssignPatientPathwayCommand.UserId), CantWorkOnPathways(username))]);

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

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
