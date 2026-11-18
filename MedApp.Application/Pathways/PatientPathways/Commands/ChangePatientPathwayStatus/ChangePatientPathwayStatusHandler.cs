using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;

public sealed class ChangePatientPathwayStatusHandler(
    IPatientPathwayRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<ChangePatientPathwayStatusCommand, PatientPathwayResponse>
{
    public async Task<PatientPathwayResponse> Handle(ChangePatientPathwayStatusCommand request, CancellationToken cancellationToken)
    {
        var to = PathwayEnums.Parse<PatientPathwayStatus>(request.Status);
        // Blocking and cancelling stay allowed while the patient's record is restricted, so care can be paused safely.
        var pathway = PatientRules.PausesWhileRestricted.Contains(to)
            ? await repository.GetForPauseOrThrowAsync(request.Id, cancellationToken)
            : await repository.GetForUpdateOrThrowAsync(request.Id, cancellationToken);

        if (!PathwayWorkflow.AllowedStatusChanges(pathway).Contains(to))
            throw new ConflictException(
                $"A pathway that's {PathwayWorkflow.Describe(pathway.Status)} can't be made {PathwayWorkflow.Describe(to)}.");

        if (to == PatientPathwayStatus.Completed && !PathwayWorkflow.AllStagesFinished(pathway))
            throw new ConflictException("Every stage must be finished before the pathway is marked discharged.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.ChangeStatus(to, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim());

        // A cancelled pathway's visits no longer need booking. Booked ones stay: someone may need to tell the patient.
        if (to == PatientPathwayStatus.Cancelled)
        {
            var requested = await appointmentRepository.GetRequestedForPathwayForUpdateAsync(pathway.Id, cancellationToken);
            foreach (var appointment in requested)
                change.CancelRequestedVisit(appointment, PathwayChange.PathwayCancelledReason);
        }

        await repository.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return await PathwayMapper.ToResponseAsync(pathway, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId, viewerPermissions: request.Actor.Permissions);
    }
}
