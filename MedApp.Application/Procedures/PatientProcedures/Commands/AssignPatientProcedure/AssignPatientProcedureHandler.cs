using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;

public sealed class AssignPatientProcedureHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    IProcedureMonitor procedureMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<AssignPatientProcedureCommand, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(AssignPatientProcedureCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);

        if (!ProcedureWorkflow.CanChangeAssignments(procedure))
            throw new ConflictException($"Staff can't be assigned to a procedure that's {ProcedureWorkflow.Describe(procedure.Status)}.");

        var usernames = await identityReadService.GetUsernamesByIdAsync([request.UserId], cancellationToken);
        if (!usernames.TryGetValue(request.UserId, out var username))
            throw new NotFoundException($"User '{request.UserId}' was not found.");

        if (procedure.Assignments.Any(a => a.UserId == request.UserId))
            throw new ConflictException($"{username} is already assigned to this procedure.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);
        change.Assign(request.UserId, username);

        await repository.SaveAsync(change.AuditEntries, cancellationToken);

        // Tell them, unless they assigned themselves (they know already).
        if (AssignmentNotifications.For(procedure, request.UserId, request.Actor, now) is { } notification)
            await notificationSender.SendAsync(notification, cancellationToken);

        // Then the admins watching, after the person it's about.
        await procedureMonitor.NotifyAsync(change, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
