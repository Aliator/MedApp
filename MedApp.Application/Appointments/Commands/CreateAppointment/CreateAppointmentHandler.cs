using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentHandler(
    IAppointmentRepository repository,
    IPatientRepository patientRepository,
    IPatientProcedureRepository procedureRepository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<CreateAppointmentCommand, AppointmentResponse>
{
    public const string StageHasOpenVisit = "This stage already has a visit requested or booked.";

    public async Task<AppointmentResponse> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        PatientProcedure? procedure = null;
        PatientProcedureStage? stage = null;
        if (request.ProcedureId is { } procedureId)
        {
            procedure = await procedureRepository.GetByIdAsync(procedureId, cancellationToken)
                        ?? throw new NotFoundException($"Procedure '{procedureId}' was not found.");

            if (procedure.PatientId != patient.Id)
                throw new ConflictException("That procedure is for another patient.");

            if (request.StageId is { } stageId)
            {
                stage = procedure.GetStageOrThrow(stageId);

                if (ProcedureWorkflow.IsClosed(procedure.Status))
                    throw new ConflictException(
                        $"A visit can't be booked for a stage of a procedure that's {ProcedureWorkflow.Describe(procedure.Status)}.");

                // One open appointment per stage.
                if (await repository.GetOpenForStageAsync(stageId, cancellationToken) is not null)
                    throw new ConflictException(StageHasOpenVisit);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        Appointment appointment;

        if (request.IsBooking)
        {
            var staffUserId = request.StaffUserId!.Value;
            var staffUsername = await identityReadService.GetUsernameOrThrowAsync(staffUserId, cancellationToken);
            var start = UkTime.Utc(request.StartUtc!.Value);
            var end = UkTime.Utc(request.EndUtc!.Value);

            // Checked and saved holding the member of staff's calendar lock, so two bookings can't race for a slot.
            appointment = await calendarLock.RunAsync([staffUserId], async ct =>
            {
                var date = UkTime.DateOf(start);
                var data = await calendarRepository.GetCalendarDataAsync(staffUserId, date, date, ct);
                if (CalendarClashes.Find(data, start, end) is { } clash)
                    throw new ConflictException(clash);

                var booked = New(request, now);
                var change = new AppointmentChange(booked, request.Actor, now, procedureTitle: procedure?.Title, stageName: stage?.Name);
                change.Book(staffUserId, staffUsername, start, end);
                await repository.AddAsync(booked, change.Events, ct);
                return booked;
            }, cancellationToken);

            foreach (var notification in AppointmentNotifications.AfterBooking(
                         appointment, procedure?.Title, AppointmentStatus.Requested, null, null, request.Actor, now))
                await notificationSender.SendAsync(notification, cancellationToken);
        }
        else
        {
            appointment = New(request, now);
            var change = new AppointmentChange(appointment, request.Actor, now, procedureTitle: procedure?.Title, stageName: stage?.Name);
            change.Request();
            await repository.AddAsync(appointment, change.Events, cancellationToken);
        }

        // Read back with its patient, procedure and stage, for the response.
        var saved = await repository.GetByIdAsync(appointment.Id, cancellationToken) ?? appointment;
        return await AppointmentMapper.ToResponseAsync(saved, identityReadService, now, cancellationToken);
    }

    private static Appointment New(CreateAppointmentCommand request, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = request.PatientId,
        ProcedureId = request.ProcedureId,
        StageId = request.StageId,
        Status = AppointmentStatus.Requested,
        Notes = request.Notes ?? string.Empty,
        IsAutomatic = false,
        CreatedAtUtc = nowUtc,
        CreatedByUserId = request.Actor.UserId,
        LastUpdated = nowUtc
    };
}
