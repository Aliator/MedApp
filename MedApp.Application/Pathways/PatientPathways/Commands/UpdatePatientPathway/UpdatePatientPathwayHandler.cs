using MedApp.Application.Common.Teams;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;

public sealed class UpdatePatientPathwayHandler(
    ITeamBoundary boundary,
    IPatientPathwayRepository repository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePatientPathwayCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(UpdatePatientPathwayCommand request, CancellationToken cancellationToken)
    {
        var pathway = await repository.GetForUpdateOrThrowAsync(request.Id, boundary, request.Actor.Teams, cancellationToken);

        if (!PathwayWorkflow.CanEditDetails(pathway))
            throw new ConflictException($"A pathway that's {PathwayWorkflow.Describe(pathway.Status)} can't be edited.");

        // While its referral's waiting-time clock runs or is paused, the clock sets the target date (decision 32).
        if (request.DueDate is { } dueDate
            && dueDate != DueDates.ToUkDate(pathway.DueDateUtc)
            && WaitingClock.SetsPathwayTarget(pathway.Referral))
            throw new ValidationException([
                new ValidationFailure(nameof(UpdatePatientPathwayCommand.DueDate), WaitingClock.TargetFromClockMessage)
            ]);

        // What changed, from and to, for the audit log.
        var changes = PathwayDetails.Apply(
            pathway,
            request.Title?.Trim(),
            request.DueDate,
            request.Priority is null ? null : PathwayEnums.Parse<PatientPathwayPriority>(request.Priority));

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (changes.Count > 0)
        {
            var change = new PathwayChange(pathway, request.Actor, now);
            PathwayDetails.Record(change, changes);

            // A new priority re-reads the clock's target, and a running clock moves the target date with it.
            if (changes.ContainsKey(PathwayDetails.PriorityField))
                change.PriorityChanged();

            await repository.SaveAsync(change, cancellationToken);
            await pathwayMonitor.NotifyAsync(change, cancellationToken);
        }

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
