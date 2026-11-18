using MedApp.Application.PatientMessages.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;

namespace MedApp.Application.PatientMessages.Repositories;

/// <summary>
/// Messages to patients (<see cref="PatientMessage"/>). It shares the request's DbContext with the appointment and
/// clinic repositories, so messages a handler stages (<see cref="Stage"/>) are saved in the same
/// <c>SaveChangesAsync</c> as the appointment change they're about.
/// </summary>
public interface IPatientMessageRepository
{
    /// <summary>Adds messages to be saved with the request's next save (the appointment change they're about).</summary>
    void Stage(IEnumerable<PatientMessage> messages);

    /// <summary>Whether the appointment already has a message of this kind about this start and room (on any channel).</summary>
    Task<bool> ExistsAsync(Guid appointmentId, PatientMessageKind kind, DateTime appointmentStartUtc, Guid? roomId, CancellationToken ct);

    /// <summary>An appointment's messages, newest first, read-only.</summary>
    Task<IReadOnlyList<PatientMessage>> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct);

    /// <summary>The sites of these rooms, as messages name them, by room.</summary>
    Task<IReadOnlyDictionary<Guid, MessageSite>> GetSitesForRoomsAsync(IEnumerable<Guid> roomIds, CancellationToken ct);

    /// <summary>
    /// Booked appointments starting after <paramref name="fromUtc"/> and no later than <paramref name="toUtc"/>, of
    /// patients whose records are open, with no reminder about that start and room yet (or only ones that couldn't go,
    /// <c>NotSent</c>, when the patient can now be reached), soonest first, with their patients.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetDueRemindersAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct);

    /// <summary>
    /// Appointments marked Did not attend that started from <paramref name="fromUtc"/> to before
    /// <paramref name="toUtc"/>, of patients whose records are open, with no missed-appointment message yet, with their
    /// patients.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetDueMissedAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct);

    /// <summary>
    /// Adds messages and saves them, in place of any <c>NotSent</c> message with the same appointment, kind, channel,
    /// start and room (a patient who has since chosen a channel, or given a number), deleted just before.
    /// </summary>
    Task AddAsync(IReadOnlyCollection<PatientMessage> messages, CancellationToken ct);

    /// <summary>Messages waiting to be sent, oldest first, tracked so <see cref="SaveAsync"/> saves changes to them.</summary>
    Task<IReadOnlyList<PatientMessage>> GetPendingAsync(int limit, CancellationToken ct);

    /// <summary>Appointments by ID, with their patients, read-only.</summary>
    Task<IReadOnlyDictionary<Guid, Appointment>> GetAppointmentsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Messages handed to the provider since <paramref name="sentSinceUtc"/> whose delivery isn't known yet, those asked
    /// about longest ago first, tracked.
    /// </summary>
    Task<IReadOnlyList<PatientMessage>> GetAwaitingDeliveryAsync(DateTime sentSinceUtc, int limit, CancellationToken ct);

    /// <summary>
    /// Saves changes to tracked messages. One that someone else changed meanwhile (anonymisation, its row version) keeps
    /// their change: it's read again, and this one's is dropped.
    /// </summary>
    Task SaveAsync(CancellationToken ct);
}
