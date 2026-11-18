using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CreateAppointment;

public sealed class CreateAppointmentHandler(
    IAppointmentRepository repository,
    IPatientRepository patientRepository,
    IPatientPathwayRepository pathwayRepository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<CreateAppointmentCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        // A restricted or anonymised record can't gain new appointments (409), as it can't gain pathways.
        PatientRules.EnsureCanChange(patient);

        PatientPathway? pathway = null;
        PatientPathwayStage? stage = null;
        if (request.PathwayId is { } pathwayId)
        {
            pathway = await pathwayRepository.GetByIdAsync(pathwayId, cancellationToken)
                        ?? throw new NotFoundException($"Pathway '{pathwayId}' was not found.");

            if (pathway.PatientId != patient.Id)
                throw new ConflictException("That pathway is for another patient.");

            if (request.StageId is { } stageId)
            {
                stage = pathway.GetStageOrThrow(stageId);

                if (PathwayWorkflow.IsClosed(pathway.Status))
                    throw new ConflictException(
                        $"A visit can't be booked for a stage of a pathway that's {PathwayWorkflow.Describe(pathway.Status)}.");

                // One open appointment per stage (decision 18). The database enforces it too, for two requests at once.
                if (await repository.GetOpenForStageAsync(stageId, cancellationToken) is { } open)
                    throw await StageHasOpenVisitAsync(open, cancellationToken);
            }
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        Appointment appointment;

        try
        {
            appointment = await AddAsync(request, pathway, stage, now, cancellationToken);
        }
        catch (StageHasOpenAppointmentException e)
        {
            // Another request gave the stage its open appointment after the check above: the same 409, pointing to it.
            throw await StageHasOpenVisitAsync(
                await repository.GetOpenForStageAsync(e.StageId, cancellationToken), cancellationToken);
        }

        // Read back with its patient, pathway and stage, for the response.
        var saved = await repository.GetByIdAsync(appointment.Id, cancellationToken) ?? appointment;
        return await AppointmentMapper.ToResponseAsync(saved, identityReadService, now, cancellationToken);
    }

    /// <summary>Adds the appointment: booked (under the member of staff's calendar lock) or Requested.</summary>
    private async Task<Appointment> AddAsync(
        CreateAppointmentCommand request,
        PatientPathway? pathway,
        PatientPathwayStage? stage,
        DateTime now,
        CancellationToken cancellationToken)
    {
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
                var change = new AppointmentChange(booked, request.Actor, now, pathwayTitle: pathway?.Title, stageName: stage?.Name);
                change.Book(staffUserId, staffUsername, start, end);
                await repository.AddAsync(booked, change.Events, ct);
                return booked;
            }, cancellationToken);

            foreach (var notification in AppointmentNotifications.AfterBooking(
                         appointment, AppointmentStatus.Requested, null, null, request.Actor, now))
                await notificationSender.SendAsync(notification, cancellationToken);
        }
        else
        {
            appointment = New(request, now);
            var change = new AppointmentChange(appointment, request.Actor, now, pathwayTitle: pathway?.Title, stageName: stage?.Name);
            change.Request();
            await repository.AddAsync(appointment, change.Events, cancellationToken);
        }

        return appointment;
    }

    /// <summary>The 409 for a stage that already has an open appointment, saying when and with whom (or that it's in Requires Appointment).</summary>
    private async Task<ConflictException> StageHasOpenVisitAsync(Appointment? open, CancellationToken cancellationToken)
    {
        var staffUsername = open is null ? null : await identityReadService.GetStaffUsernameAsync(open, cancellationToken);
        return new ConflictException(AppointmentWorkflow.StageHasOpenVisit(open, staffUsername));
    }

    private static Appointment New(CreateAppointmentCommand request, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = request.PatientId,
        PathwayId = request.PathwayId,
        StageId = request.StageId,
        Status = AppointmentStatus.Requested,
        Notes = request.Notes ?? string.Empty,
        IsAutomatic = false,
        CreatedAtUtc = nowUtc,
        CreatedByUserId = request.Actor.UserId,
        LastUpdated = nowUtc
    };
}
