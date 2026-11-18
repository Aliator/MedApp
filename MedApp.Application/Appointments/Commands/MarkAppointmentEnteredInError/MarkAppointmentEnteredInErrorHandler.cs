using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;

public sealed class MarkAppointmentEnteredInErrorHandler(
    IAppointmentRepository repository,
    IPatientProcedureRepository procedureRepository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<MarkAppointmentEnteredInErrorCommand, EnteredInErrorResponse>
{
    /// <summary>Why no visit is asked for again on a restricted record, which can't gain appointments.</summary>
    public const string RestrictedMessage = "No visit is asked for again while the record is restricted.";

    public async Task<EnteredInErrorResponse> Handle(MarkAppointmentEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        // Allowed while the record is restricted (correcting a wrong entry is rectification, as cancelling is a pause);
        // refused once it's anonymised.
        var appointment = await repository.GetForCancelOrThrowAsync(request.AppointmentId, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!request.IsAdmin && appointment.BookedByUserId != request.Actor.UserId && appointment.IsRecordedAfterEvent)
            throw new ForbiddenException(AppointmentWorkflow.NotYourRecordMessage);

        // Admins at any time; whoever recorded it within the window it could be recorded in.
        if (AppointmentWorkflow.WhyCantMarkEnteredInError(appointment, now, request.IsAdmin) is { } why)
            throw new ConflictException(why);

        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);
        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.MarkEnteredInError(request.Reason.Trim());

        var stage = await AskForTheStagesVisitAgainAsync(appointment, request, now, cancellationToken);
        if (stage.Requested is { } requested)
        {
            try
            {
                await repository.AddAsync(requested.Appointment, [.. change.Events, .. requested.Events], cancellationToken);
            }
            catch (StageHasOpenAppointmentException)
            {
                // Someone booked or asked for the stage's visit between reading its appointments and saving: nothing
                // was saved, so the user tries again, and then sees the visit there is.
                throw new ConflictException(AppointmentWorkflow.RequestedVisitChangedMeanwhileMessage);
            }
        }
        else
        {
            await repository.SaveAsync(change.Events, cancellationToken);
        }

        // A finished stage that requires a visit, left without one: only an admin can put it right, so the procedure's
        // assignees and the admins are told, not only the person withdrawing it.
        if (stage.FinishedWithoutVisit is (var procedure, var finished))
        {
            var admins = await identityReadService.GetAdminIdsAsync(cancellationToken);
            foreach (var notification in AppointmentNotifications.AfterStageVisitWithdrawn(appointment, procedure, finished, admins, request.Actor, now))
                await notificationSender.SendAsync(notification, cancellationToken);
        }

        return new EnteredInErrorResponse
        {
            Appointment = await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken),
            VisitRequestedAgain = stage.Requested is not null,
            StageMessage = stage.Message
        };
    }

    /// <summary>What withdrawing a stage's visit does to the stage.</summary>
    /// <param name="Requested">The Requested visit to add (with its history), saved in the same save as the withdrawal.</param>
    /// <param name="Message">What to tell the person withdrawing it about the stage, if anything.</param>
    /// <param name="FinishedWithoutVisit">The procedure and stage, when a finished stage that requires a visit is left without one.</param>
    private sealed record StageOutcome(
        (Appointment Appointment, IReadOnlyList<AppointmentEvent> Events)? Requested,
        string? Message,
        (PatientProcedure Procedure, PatientProcedureStage Stage)? FinishedWithoutVisit);

    /// <summary>
    /// For a stage's visit, whether to ask for it again (by <see cref="StageVisits.AfterVisitWithdrawn"/>'s rules, and
    /// never on a record that can't change, such as a restricted one), and what to say about the stage.
    /// </summary>
    private async Task<StageOutcome> AskForTheStagesVisitAgainAsync(
        Appointment withdrawn, MarkAppointmentEnteredInErrorCommand request, DateTime now, CancellationToken ct)
    {
        if (withdrawn is not { ProcedureId: { } procedureId, StageId: { } stageId })
            return new StageOutcome(null, null, null);

        var procedure = await procedureRepository.GetByIdAsync(procedureId, ct);
        var stage = procedure?.Stages.FirstOrDefault(s => s.Id == stageId);
        if (procedure is null || stage is null)
            return new StageOutcome(null, null, null);

        var appointments = await repository.GetForStagesForUpdateAsync([stageId], ct);
        var (again, message, finishedWithoutVisit) =
            StageVisits.AfterVisitWithdrawn(procedure, stage, appointments.Where(a => a.Id != withdrawn.Id).Append(withdrawn));
        var finished = finishedWithoutVisit ? (procedure, stage) : ((PatientProcedure, PatientProcedureStage)?)null;
        if (!again)
            return new StageOutcome(null, message, finished);

        // A restricted (or anonymised) record can't gain appointments, so the withdrawal goes ahead without one.
        if (!PatientRules.CanChange(withdrawn.Patient))
            return new StageOutcome(null, RestrictedMessage, finished);

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
        return new StageOutcome((visit, change.Events), message, finished);
    }
}
