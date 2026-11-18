using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specialties.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Appointments;
using MediatR;

namespace MedApp.Application.Clinics.Commands.UpdateClinicSession;

public sealed class UpdateClinicSessionHandler(
    IClinicRepository repository,
    ICalendarRepository calendarRepository,
    ISpecialtyRepository specialtyRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateClinicSessionCommand, ClinicSessionResponse>
{
    public async Task<ClinicSessionResponse> Handle(UpdateClinicSessionCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetSessionAsync(request.SessionId, cancellationToken)
                       ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");

        // Compared here first, so a stale change fails without taking a lock.
        var expected = UkTime.Utc(request.ExpectedLastUpdated!.Value);
        ClinicRules.EnsureUnchanged(existing, expected);
        ClinicRules.EnsurePlanned(existing);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        CalendarGuards.EnsureNotPast(existing.Date, today);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(start), today);
        await ClinicChoices.CheckSpecialtyAsync(specialtyRepository, request.SpecialtyId, existing.SpecialtyId, cancellationToken);

        var staffUsername = await identityReadService.GetUsernamesByIdAsync([existing.StaffUserId], cancellationToken);

        // Holding the clinician's lock and both rooms' (bookings into it take the clinician's).
        var locks = SessionPlacement.Locks(existing.StaffUserId, existing.RoomId, request.RoomId);
        var bookedStarts = await calendarLock.RunAsync(locks, async ct =>
        {
            var session = await repository.GetSessionForUpdateAsync(request.SessionId, ct)
                          ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");
            ClinicRules.EnsureUnchanged(session, expected);
            ClinicRules.EnsurePlanned(session);

            var appointments = await repository.GetSessionAppointmentsForUpdateAsync(session.Id, ct);
            var moved = start != session.StartUtc || end != session.EndUtc || request.SlotMinutes != session.SlotMinutes;
            if (moved && appointments.Count > 0)
                throw new ConflictException(ClinicRules.HasBookings);

            if (moved && start <= now)
                throw new ConflictException(ClinicRules.InThePast);

            var roomChanged = request.RoomId != session.RoomId;
            if (roomChanged)
                await ClinicChoices.CheckRoomAsync(repository, request.RoomId, UkTime.DateOf(start), session.RoomId, ct);

            if ((moved || roomChanged)
                && await SessionPlacement.WhyNotAsync(
                    calendarRepository, repository, session.StaffUserId, request.RoomId, start, end, ct, session.Id, checkPerson: moved) is { } why)
                throw new ConflictException(why);

            session.RoomId = request.RoomId;
            session.SpecialtyId = request.SpecialtyId;
            session.StartUtc = start;
            session.EndUtc = end;
            session.Date = UkTime.DateOf(start);
            session.SlotMinutes = request.SlotMinutes;
            session.LastUpdated = now;

            // Its Booked appointments move to its new room, each recorded in its history (a move, with the room before);
            // those already attended or missed happened in the room they name, so they keep it.
            var events = new List<AppointmentEvent>();
            var staffName = staffUsername.GetValueOrDefault(session.StaffUserId) ?? string.Empty;
            var actor = new PathwayActor(request.Actor.UserId, request.Actor.Username, request.Actor.Permissions);
            foreach (var appointment in appointments.Where(a => a.Status == AppointmentStatus.Booked && a.RoomId != request.RoomId))
            {
                var change = new AppointmentChange(appointment, actor, now, staffName);
                change.Book(session.StaffUserId, staffName, appointment.StartUtc!.Value, appointment.EndUtc!.Value, new AppointmentPlace(session.Id, request.RoomId));
                events.AddRange(change.Events);
            }

            await repository.SaveAsync(events, ClinicRules.ChangedMeanwhile, ct);
            return appointments.Where(a => a.StartUtc is not null).Select(a => a.StartUtc!.Value).ToList();
        }, cancellationToken);

        var saved = await repository.GetSessionAsync(request.SessionId, cancellationToken)
                    ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");
        return ClinicMapper.ToResponse(
            saved, staffUsername.GetValueOrDefault(saved.StaffUserId) ?? string.Empty, bookedStarts, now, request.Actor.CanManageClinics);
    }
}
