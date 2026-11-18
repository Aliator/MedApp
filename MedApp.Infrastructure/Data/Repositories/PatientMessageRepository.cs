using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Contacts;
using MedApp.Domain.PatientMessages;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Messages to patients. Staged messages share the request's DbContext with the appointment and clinic repositories,
/// so they're saved with the appointment change they're about.
/// </summary>
public sealed class PatientMessageRepository(MedAppDbContext context) : IPatientMessageRepository
{
    public void Stage(IEnumerable<PatientMessage> messages) => context.PatientMessages.AddRange(messages);

    public async Task<bool> ExistsAsync(Guid appointmentId, PatientMessageKind kind, DateTime appointmentStartUtc, CancellationToken ct) =>
        await context.PatientMessages.AnyAsync(
            m => m.AppointmentId == appointmentId && m.Kind == kind && m.AppointmentStartUtc == appointmentStartUtc, ct);

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
                        && !context.PatientMessages.Any(m => m.AppointmentId == a.Id
                                                             && m.Kind == PatientMessageKind.AppointmentReminder
                                                             && m.AppointmentStartUtc == a.StartUtc))
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

    public Task SaveAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
