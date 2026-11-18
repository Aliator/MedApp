using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Contacts;
using MedApp.Domain.PatientMessages;
using MedApp.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Messages to patients. Staged messages share the request's DbContext with the appointment and clinic repositories,
/// so they're saved with the appointment change they're about.
/// </summary>
public sealed class PatientMessageRepository(MedAppDbContext context) : IPatientMessageRepository
{
    public void Stage(IEnumerable<PatientMessage> messages) => context.PatientMessages.AddRange(messages);

    public async Task<bool> ExistsAsync(
        Guid appointmentId, PatientMessageKind kind, DateTime appointmentStartUtc, Guid? roomId, CancellationToken ct) =>
        await context.PatientMessages.AnyAsync(
            m => m.AppointmentId == appointmentId && m.Kind == kind && m.AppointmentStartUtc == appointmentStartUtc
                 && m.AppointmentRoomId == roomId, ct);

    public async Task<IReadOnlyList<PatientMessage>> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        await context.PatientMessages
            .AsNoTracking()
            .Where(m => m.AppointmentId == appointmentId)
            .OrderByDescending(m => m.CreatedAtUtc).ThenBy(m => m.Channel).ThenBy(m => m.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, MessageSite>> GetSitesForRoomsAsync(IEnumerable<Guid> roomIds, CancellationToken ct)
    {
        var ids = roomIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, MessageSite>();

        var rooms = await context.Rooms
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.Site.Name, r.Site.AddressLines, r.Site.Postcode, r.Site.Phone })
            .ToListAsync(ct);

        return rooms.ToDictionary(
            r => r.Id,
            r => new MessageSite(r.Name, PostalAddress.Split(r.AddressLines), r.Postcode, r.Phone));
    }

    public async Task<IReadOnlyList<Appointment>> GetDueRemindersAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct) =>
        await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.Status == AppointmentStatus.Booked
                        && a.StartUtc > fromUtc
                        && a.StartUtc <= toUtc
                        && a.Patient.RestrictedAtUtc == null
                        && a.Patient.AnonymisedAtUtc == null
                        // None about this start and room yet, but for ones that couldn't go when the patient can
                        // now be reached (they've chosen a channel, or given its number or address since).
                        && !context.PatientMessages.Any(m => m.AppointmentId == a.Id
                                                             && m.Kind == PatientMessageKind.AppointmentReminder
                                                             && m.AppointmentStartUtc == a.StartUtc
                                                             && m.AppointmentRoomId == a.RoomId
                                                             && (m.Status != PatientMessageStatus.NotSent
                                                                 || !(((a.Patient.ReminderChannel == ReminderChannel.Text || a.Patient.ReminderChannel == ReminderChannel.TextAndEmail)
                                                                       && a.Patient.MobilePhone != null)
                                                                      || ((a.Patient.ReminderChannel == ReminderChannel.Email || a.Patient.ReminderChannel == ReminderChannel.TextAndEmail)
                                                                          && a.Patient.Email.Contains("@"))))))
            .OrderBy(a => a.StartUtc).ThenBy(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetDueMissedAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct) =>
        await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.Status == AppointmentStatus.DidNotAttend
                        && a.StartUtc >= fromUtc
                        && a.StartUtc < toUtc
                        && a.Patient.RestrictedAtUtc == null
                        && a.Patient.AnonymisedAtUtc == null
                        && !context.PatientMessages.Any(m => m.AppointmentId == a.Id
                                                             && m.Kind == PatientMessageKind.MissedAppointment))
            .OrderBy(a => a.StartUtc).ThenBy(a => a.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(IReadOnlyCollection<PatientMessage> messages, CancellationToken ct)
    {
        // Ones that couldn't go, with the same key as a new one, make way for it (deleted first: the unique index).
        var appointmentIds = messages.Select(m => m.AppointmentId).Distinct().ToList();
        var notSent = await context.PatientMessages
            .Where(m => appointmentIds.Contains(m.AppointmentId) && m.Status == PatientMessageStatus.NotSent)
            .ToListAsync(ct);
        var replaced = notSent
            .Where(old => messages.Any(m => m.AppointmentId == old.AppointmentId && m.Kind == old.Kind && m.Channel == old.Channel
                                            && m.AppointmentStartUtc == old.AppointmentStartUtc && m.AppointmentRoomId == old.AppointmentRoomId))
            .ToList();
        context.PatientMessages.RemoveRange(replaced);
        if (replaced.Count > 0)
            await context.SaveChangesAsync(ct);

        context.PatientMessages.AddRange(messages);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PatientMessage>> GetPendingAsync(int limit, CancellationToken ct) =>
        await context.PatientMessages
            .Where(m => m.Status == PatientMessageStatus.Pending)
            .OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, Appointment>> GetAppointmentsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await context.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => list.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
    }

    public async Task<IReadOnlyList<PatientMessage>> GetAwaitingDeliveryAsync(DateTime sentSinceUtc, int limit, CancellationToken ct) =>
        await context.PatientMessages
            .Where(m => m.Status == PatientMessageStatus.Sent
                        && m.ProviderReference != null
                        && m.SentAtUtc >= sentSinceUtc)
            .OrderBy(m => m.StatusAtUtc).ThenBy(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

    public async Task SaveAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await context.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException e) when (attempt < 5)
            {
                // Someone else's change (an anonymisation cancelling it, replacing its destination) wins.
                foreach (var entry in e.Entries)
                    await entry.ReloadAsync(ct);
            }
        }
    }
}
