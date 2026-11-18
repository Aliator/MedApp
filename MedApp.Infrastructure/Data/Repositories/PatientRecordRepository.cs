using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// A patient's whole record, from every table that holds anything about them. Anything new that holds patient data
/// must be added here, so subject access exports and anonymisation cover it.
/// </summary>
public sealed class PatientRecordRepository(MedAppDbContext context) : IPatientRecordRepository
{
    public async Task<PatientRecord?> GetAsync(Guid patientId, bool forUpdate, CancellationToken ct)
    {
        var patients = forUpdate ? context.Patients : context.Patients.AsNoTracking();
        var patient = await patients.FirstOrDefaultAsync(p => p.Id == patientId, ct);
        if (patient is null)
            return null;

        var pathways = await Tracking(context.PatientPathways, forUpdate)
            .Include(p => p.Assignments)
            .Include(p => p.Stages.OrderBy(s => s.StageOrder))
            .Include(p => p.Notes)
            .Where(p => p.PatientId == patientId)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Keeps the patient with each pathway, as the mappers expect, also when read without tracking.
        foreach (var pathway in pathways)
            pathway.Patient = patient;

        var history = await Tracking(context.PatientPathwayAuditEntries, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        // Every version of every note, those of deleted pathways included.
        var noteVersions = await Tracking(context.PatientPathwayNoteVersions, forUpdate)
            .Where(v => v.PatientId == patientId)
            .OrderBy(v => v.AtUtc).ThenBy(v => v.Version).ThenBy(v => v.Id)
            .ToListAsync(ct);

        // Requests to delete notes, with their reasons, those about deleted pathways included.
        var noteDeletionRequests = await Tracking(context.PathwayNoteDeletionRequests, forUpdate)
            .Where(r => r.PatientId == patientId)
            .OrderBy(r => r.RequestedAtUtc).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var activity = await Tracking(context.ActivityEntries, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        var appointments = await Tracking(context.Appointments, forUpdate)
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);

        var appointmentHistory = await Tracking(context.AppointmentEvents, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        // Notifications point at pathways, including ones since deleted, which only the audit still names, at
        // appointments, or at referrals.
        var pathwayIds = pathways.Select(p => p.Id)
            .Concat(history.Select(e => e.PatientPathwayId))
            .Distinct()
            .ToList();
        var appointmentIds = appointments.Select(a => a.Id)
            .Concat(appointmentHistory.Select(e => e.AppointmentId))
            .Distinct()
            .ToList();
        var referralIds = await context.Referrals
            .Where(r => r.PatientId == patientId)
            .Select(r => r.Id)
            .ToListAsync(ct);
        var notifications = await Tracking(context.Notifications, forUpdate)
            .Where(n => (n.PathwayId != null && pathwayIds.Contains(n.PathwayId.Value))
                        || (n.AppointmentId != null && appointmentIds.Contains(n.AppointmentId.Value))
                        || (n.ReferralId != null && referralIds.Contains(n.ReferralId.Value)))
            .OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            .ToListAsync(ct);

        // Every file, deleted ones too: their records are kept, with their names, until the bytes are purged.
        var attachments = await Tracking(context.Attachments, forUpdate)
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.UploadedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);

        // Referrals, with what the referrer sent and what was decided, their clocks' pauses, and their history.
        var referrals = await Tracking(context.Referrals, forUpdate)
            .Include(r => r.Specialty)
            .Include(r => r.Pauses)
            .Where(r => r.PatientId == patientId)
            .OrderBy(r => r.ReceivedOn).ThenBy(r => r.RegisteredAtUtc).ThenBy(r => r.Id)
            .ToListAsync(ct);
        foreach (var referral in referrals)
            referral.Patient = patient;

        var referralHistory = await Tracking(context.ReferralEvents, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        return new PatientRecord
        {
            Patient = patient,
            Pathways = pathways,
            PathwayHistory = history,
            NoteVersions = noteVersions,
            NoteDeletionRequests = noteDeletionRequests,
            Activity = activity,
            Appointments = appointments,
            AppointmentHistory = appointmentHistory,
            Notifications = notifications,
            Attachments = attachments,
            Referrals = referrals,
            ReferralHistory = referralHistory
        };
    }

    public async Task SaveAsync(ActivityEntry activity, IReadOnlyCollection<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(activity, ct);
    }

    public async Task SaveAsync(ActivityEntry activity, CancellationToken ct)
    {
        context.ActivityEntries.Add(activity);
        await context.SaveChangesOrConflictAsync(ct);
    }

    private static IQueryable<T> Tracking<T>(DbSet<T> set, bool forUpdate) where T : class =>
        forUpdate ? set : set.AsNoTracking();
}
