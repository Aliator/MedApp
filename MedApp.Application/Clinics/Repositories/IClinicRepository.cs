using MedApp.Application.Clinics.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;

namespace MedApp.Application.Clinics.Repositories;

/// <summary>
/// Clinic sessions and patterns (centre plan decisions 123 to 127). Sessions are loaded with their room (and its site)
/// and specialty. Changes that put a session on a calendar run inside <c>ICalendarLock</c>, holding the clinician's and
/// the room's locks, whose transaction these share.
/// </summary>
public interface IClinicRepository
{
    /// <summary>A session with its room, site and specialty, read-only.</summary>
    Task<ClinicSession?> GetSessionAsync(Guid id, CancellationToken ct);

    /// <summary>A session with its room, site and specialty, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<ClinicSession?> GetSessionForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Sessions starting on the filter's dates, by start then room's name, with their rooms, sites and specialties.</summary>
    Task<IReadOnlyList<ClinicSession>> SearchSessionsAsync(ClinicSessionFilter filter, CancellationToken ct);

    /// <summary>The start of every appointment on the calendar (Booked, Attended or Did not attend) in each of these sessions.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<DateTime>>> GetBookedStartsAsync(IEnumerable<Guid> sessionIds, CancellationToken ct);

    /// <summary>
    /// A session's appointments on the calendar (Booked, Attended or Did not attend) and, once it's cancelled, those
    /// cancelled with it (the patients to tell), with their patients, read-only.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetSessionAppointmentsAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// A session's appointments on the calendar (Booked, Attended or Did not attend), with their patients, pathways and
    /// stages, tracked so <see cref="SaveAsync"/> saves changes to them.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetSessionAppointmentsForUpdateAsync(Guid sessionId, CancellationToken ct);

    /// <summary>What a room has on a UK date (see <see cref="RoomDay"/>), or null if there's no such room.</summary>
    Task<RoomDay?> GetRoomDayAsync(Guid roomId, DateOnly date, CancellationToken ct);

    /// <summary>
    /// Adds a session. A session its pattern already made on that date (another request, just now) is a
    /// <see cref="Common.Exceptions.ConflictException"/>.
    /// </summary>
    Task AddSessionAsync(ClinicSession session, CancellationToken ct);

    /// <summary>
    /// Saves changes to sessions, patterns and appointments loaded for update, with these appointment history entries, in
    /// one save. Something someone else saved since it was loaded (its row version) is a 409
    /// (<paramref name="conflictMessage"/>), and nothing is saved.
    /// </summary>
    Task SaveAsync(IEnumerable<AppointmentEvent> events, string conflictMessage, CancellationToken ct);

    /// <summary>The dates a pattern has made sessions on (in any status) from <paramref name="from"/> to <paramref name="to"/>.</summary>
    Task<IReadOnlySet<DateOnly>> GetPatternDatesAsync(Guid patternId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Every pattern, with its room, site and specialty, read-only.</summary>
    Task<IReadOnlyList<ClinicPattern>> GetPatternsAsync(CancellationToken ct);

    /// <summary>A pattern with its room, site and specialty, read-only.</summary>
    Task<ClinicPattern?> GetPatternAsync(Guid id, CancellationToken ct);

    /// <summary>A pattern, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<ClinicPattern?> GetPatternForUpdateAsync(Guid id, CancellationToken ct);

    Task AddPatternAsync(ClinicPattern pattern, CancellationToken ct);

    /// <summary>How many Planned sessions a member of staff has that haven't ended by <paramref name="nowUtc"/>.</summary>
    Task<int> CountUpcomingForStaffAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct);
}
