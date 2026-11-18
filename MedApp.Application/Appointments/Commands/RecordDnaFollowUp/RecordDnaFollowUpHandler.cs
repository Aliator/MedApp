using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MediatR;

namespace MedApp.Application.Appointments.Commands.RecordDnaFollowUp;

/// <summary>
/// Records a Did not attend's follow-up through <see cref="AppointmentChange"/>, so its history has it. Only a Did not
/// attend (409 otherwise); a restricted or anonymised record can't change (409), as for every appointment change.
/// </summary>
public sealed class RecordDnaFollowUpHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<RecordDnaFollowUpCommand, AppointmentResponse>
{
    public const string NotMissedMessage = "A follow-up is recorded only for an appointment marked Did not attend.";

    public async Task<AppointmentResponse> Handle(RecordDnaFollowUpCommand request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetForUpdateOrThrowAsync(request.AppointmentId, cancellationToken);
        if (!AppointmentWorkflow.CanRecordDnaFollowUp(appointment))
            throw new ConflictException(NotMissedMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);
        var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();

        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.RecordDnaFollowUp(PathwayEnums.Parse<DnaFollowUpDecision>(request.Decision), detail);

        if (change.HasChanges)
            await repository.SaveAsync(change.Events, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken);
    }
}
