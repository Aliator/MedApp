using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;

public sealed class UpdateAppointmentNotesHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateAppointmentNotesCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(UpdateAppointmentNotesCommand request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetForUpdateOrThrowAsync(request.AppointmentId, cancellationToken);

        if (!AppointmentWorkflow.CanEditNotes(appointment))
            throw new ConflictException("A cancelled appointment's notes can't be changed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);

        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.UpdateNotes(request.Notes);

        if (change.HasChanges)
            await repository.SaveAsync(change.Events, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken);
    }
}
