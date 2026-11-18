using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MediatR;

namespace MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;

public sealed class RecordAppointmentOutcomeHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<RecordAppointmentOutcomeCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(RecordAppointmentOutcomeCommand request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetForUpdateOrThrowAsync(request.AppointmentId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (AppointmentWorkflow.WhyOutcomeCantBeRecorded(appointment, now) is { } reason)
            throw new ConflictException(reason);

        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);
        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.RecordOutcome(PathwayEnums.Parse<AppointmentStatus>(request.Outcome));

        if (change.HasChanges)
            await repository.SaveAsync(change.Events, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken);
    }
}
