using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;

namespace MedApp.Application.Appointments.Common;

/// <summary>Where a booked appointment is: a clinic session's slot (and its room), a room on its own, or neither.</summary>
public sealed record AppointmentPlace(Guid? ClinicSessionId, Guid? RoomId)
{
    public static readonly AppointmentPlace None = new(null, null);
}

/// <summary>
/// Booking into a clinic session's slot, or into a room (centre plan decision 124). Into a session: it must be Planned,
/// the member of staff must be its clinician, the time exactly one of its slots, and its room's site not closed that
/// day; the appointment takes its room, and
/// the clinician's calendar lock serialises the slot (with <c>CalendarClashes</c>, which then sees any appointment
/// already in it, and the database's unique index on session and start). Into a room outside a session: the room must
/// be open and free, under the room's lock too.
/// </summary>
public static class AppointmentPlaces
{
    /// <summary>The locks a booking takes: the member of staff's, and a room's when it names one outside a session.</summary>
    public static IReadOnlyCollection<Guid> Locks(Guid staffUserId, Guid? clinicSessionId, Guid? roomId) =>
        clinicSessionId is null && roomId is { } room ? [staffUserId, room] : [staffUserId];

    /// <summary>
    /// Checks the place, holding <see cref="Locks"/>: a 404 for an unknown session or room, a 409 for anything else
    /// wrong (see the class). <paramref name="current"/> is the appointment being moved, if any (its own room may be
    /// archived since, and it's left out of the room's clash check).
    /// </summary>
    public static async Task<AppointmentPlace> CheckAsync(
        IClinicRepository clinics,
        Guid staffUserId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? clinicSessionId,
        Guid? roomId,
        Appointment? current,
        CancellationToken ct)
    {
        if (clinicSessionId is { } sessionId)
        {
            var session = await clinics.GetSessionAsync(sessionId, ct)
                          ?? throw new NotFoundException($"Clinic session '{sessionId}' was not found.");
            if (session.Status != ClinicSessionStatus.Planned)
                throw new ConflictException(ClinicRules.SessionCancelled);
            if (session.StaffUserId != staffUserId)
                throw new ConflictException(ClinicRules.NotSessionsClinician);
            if (roomId is not null && roomId != session.RoomId)
                throw new ConflictException(ClinicRules.NotSessionsRoom);
            if (!ClinicRules.IsSlot(session, startUtc, endUtc))
                throw new ConflictException(ClinicRules.NotASlot(session));

            // The session holds its room, but a site closed since closes its rooms for bookings.
            if (await clinics.GetRoomDayAsync(session.RoomId, session.Date, ct) is { } roomDay
                && ClinicRules.WhyClosed(roomDay) is { } closed)
                throw new ConflictException(closed);

            return new AppointmentPlace(session.Id, session.RoomId);
        }

        if (roomId is not { } chosenRoom)
            return AppointmentPlace.None;

        var date = UkTime.DateOf(UkTime.Utc(startUtc));
        var day = await clinics.GetRoomDayAsync(chosenRoom, date, ct)
                  ?? throw new NotFoundException($"Room '{chosenRoom}' was not found.");
        if (day.Room.IsArchived && current?.RoomId != chosenRoom)
            throw new ConflictException(ClinicRules.RoomArchived(day.Room));
        if (ClinicRules.WhyRoomUnavailable(day, startUtc, endUtc, ignoreAppointmentId: current?.Id) is { } why)
            throw new ConflictException(why);

        return new AppointmentPlace(null, chosenRoom);
    }
}
