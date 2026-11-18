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

namespace MedApp.Application.Appointments.Commands.RecordPastAppointment;

/// <summary>
/// Records an appointment after the event (a walk-in). Through <see cref="AppointmentChange.RecordAfterEvent"/>, so
/// its history says who recorded it and when. For a stage, its visit in Requires Appointment (Requested) becomes the recorded
/// one rather than being cancelled beside a new one: the stage keeps one appointment per visit, its history reads
/// "requested, then recorded after the event", and an Attended one is the visit a Required stage needs before it's
/// finished. A stage with a visit Booked is a 409: that one should be given its outcome, or cancelled, first.
/// </summary>
public sealed class RecordPastAppointmentHandler(
    IAppointmentRepository repository,
    IPatientRepository patientRepository,
    IPatientPathwayRepository pathwayRepository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<RecordPastAppointmentCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(RecordPastAppointmentCommand request, CancellationToken cancellationToken)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");

        // A restricted or anonymised record can't gain appointments (409), recorded or booked.
        PatientRules.EnsureCanChange(patient);

        PatientPathway? pathway = null;
        PatientPathwayStage? stage = null;
        Appointment? requestedVisit = null;
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
                        $"A visit can't be recorded for a stage of a pathway that's {PathwayWorkflow.Describe(pathway.Status)}.");

                if (await repository.GetOpenForStageAsync(stageId, cancellationToken) is { } open)
                {
                    if (open.Status == AppointmentStatus.Booked)
                        throw new ConflictException(AppointmentWorkflow.StageHasBookedVisit(
                            open, await identityReadService.GetStaffUsernameAsync(open, cancellationToken)));

                    requestedVisit = open;
                }
            }
        }

        var start = UkTime.Utc(request.StartUtc);
        // By UK day, so a visit earlier on the day the pathway was created (seen at 10:00, created at 10:40) counts.
        if (pathway is not null && AppointmentWorkflow.IsBeforePathway(start, pathway.CreatedAt))
            throw new ConflictException(AppointmentWorkflow.BeforePathwayMessage(pathway.CreatedAt));
        if (pathway is not null && stage is not null && AppointmentWorkflow.WhyStageNotReached(pathway, stage) is { } notReached)
            throw new ConflictException(notReached);

        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        var end = UkTime.Utc(request.EndUtc);
        var outcome = PathwayEnums.Parse<AppointmentStatus>(request.Outcome);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Checked and saved holding the member of staff's calendar lock, so it can't race a booking for the same time.
        var recorded = await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var date = UkTime.DateOf(start);
            var data = await calendarRepository.GetCalendarDataAsync(request.StaffUserId, date, date, ct);
            if (CalendarClashes.FindAppointment(data, start, end) is { } clash)
                throw new ConflictException(clash);

            if (requestedVisit is not null)
            {
                // Loaded again for update: someone may have booked or cancelled it since. Its row version refuses a
                // save that overlaps another.
                var visit = await repository.GetForUpdateOrThrowAsync(requestedVisit.Id, ct);
                if (visit.Status != AppointmentStatus.Requested)
                    throw new ConflictException(AppointmentWorkflow.RequestedVisitChangedMeanwhileMessage);

                var change = new AppointmentChange(visit, request.Actor, now);
                change.RecordAfterEvent(request.StaffUserId, staffUsername, start, end, outcome, wasRequested: true);
                // The visit's own notes ("Needs an interpreter") are kept; new ones go after them.
                if (AppointmentWorkflow.KeepingNotes(visit.Notes, request.Notes) is { } notes)
                    change.UpdateNotes(notes);

                await repository.SaveAsync(change.Events, ct);
                return visit;
            }

            // A visit booked or requested for the stage since the check above (another request, or the stage just
            // becoming current): refused, rather than leaving the stage with two (decision 77). Another person's
            // booking takes their lock, not this one, so this narrows the window rather than closing it.
            if (stage is not null && await repository.GetOpenForStageAsync(stage.Id, ct) is not null)
                throw new ConflictException(AppointmentWorkflow.RequestedVisitChangedMeanwhileMessage);

            var appointment = New(request, now);
            var added = new AppointmentChange(appointment, request.Actor, now, pathwayTitle: pathway?.Title, stageName: stage?.Name);
            added.RecordAfterEvent(request.StaffUserId, staffUsername, start, end, outcome);
            await repository.AddAsync(appointment, added.Events, ct);
            return appointment;
        }, cancellationToken);

        foreach (var notification in AppointmentNotifications.AfterRecording(recorded, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        // Read back with its patient, pathway and stage, for the response.
        var saved = await repository.GetByIdAsync(recorded.Id, cancellationToken) ?? recorded;
        return await AppointmentMapper.ToResponseAsync(saved, identityReadService, now, cancellationToken);
    }

    private static Appointment New(RecordPastAppointmentCommand request, DateTime nowUtc) => new()
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
