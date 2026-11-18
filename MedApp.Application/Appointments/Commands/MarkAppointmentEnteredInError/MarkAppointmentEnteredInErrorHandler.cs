using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MediatR;

namespace MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;

public sealed class MarkAppointmentEnteredInErrorHandler(
    IAppointmentRepository repository,
    IPatientProcedureRepository procedureRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<MarkAppointmentEnteredInErrorCommand, EnteredInErrorResponse>
{
    public async Task<EnteredInErrorResponse> Handle(MarkAppointmentEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        // Allowed while the record is restricted (correcting a wrong entry is rectification, as cancelling is a pause);
        // refused once it's anonymised.
        var appointment = await repository.GetForCancelOrThrowAsync(request.AppointmentId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!request.IsAdmin && appointment.BookedByUserId != request.Actor.UserId && appointment.IsRecordedAfterEvent)
            throw new ForbiddenException(AppointmentWorkflow.NotYourRecordMessage);

        if (AppointmentWorkflow.WhyCantMarkEnteredInError(appointment, now) is { } why)
            throw new ConflictException(why);

        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);
        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.MarkEnteredInError(request.Reason.Trim());

        var (requested, message) = await AskForTheStagesVisitAgainAsync(appointment, request, now, cancellationToken);
        if (requested is not null)
            await repository.AddAsync(requested.Value.Appointment, [.. change.Events, .. requested.Value.Events], cancellationToken);
        else
            await repository.SaveAsync(change.Events, cancellationToken);

        return new EnteredInErrorResponse
        {
            Appointment = await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken),
            VisitRequestedAgain = requested is not null,
            StageMessage = message
        };
    }

    /// <summary>
    /// For a stage's visit, the Requested visit to add (with its history) when the stage would ask for one now, and what
    /// to say about the stage. Saved in the same save as the withdrawal.
    /// </summary>
    private async Task<((Appointment Appointment, IReadOnlyList<AppointmentEvent> Events)? Requested, string? Message)> AskForTheStagesVisitAgainAsync(
        Appointment withdrawn, MarkAppointmentEnteredInErrorCommand request, DateTime now, CancellationToken ct)
    {
        if (withdrawn is not { ProcedureId: { } procedureId, StageId: { } stageId })
            return (null, null);

        var procedure = await procedureRepository.GetByIdAsync(procedureId, ct);
        var stage = procedure?.Stages.FirstOrDefault(s => s.Id == stageId);
        if (procedure is null || stage is null)
            return (null, null);

        var appointments = await repository.GetForStagesForUpdateAsync([stageId], ct);
        var (again, message) = StageVisits.AfterVisitWithdrawn(procedure, stage, appointments.Where(a => a.Id != withdrawn.Id).Append(withdrawn));
        if (!again)
            return (null, message);

        var visit = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = procedure.PatientId,
            ProcedureId = procedure.Id,
            StageId = stage.Id,
            Status = AppointmentStatus.Requested,
            IsAutomatic = true,
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId,
            LastUpdated = now
        };
        // A tick after the withdrawal's own entry, so the history reads in order.
        var change = new AppointmentChange(visit, request.Actor, now.AddTicks(1), procedureTitle: procedure.Title, stageName: stage.Name);
        change.Request(automatic: true);
        visit.LastUpdated = now;
        return ((visit, change.Events), message);
    }
}
