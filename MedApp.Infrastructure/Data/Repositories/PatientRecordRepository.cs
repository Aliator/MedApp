using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Procedures.PatientProcedures;
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

        var procedures = await Tracking(context.PatientProcedures, forUpdate)
            .Include(p => p.Assignments)
            .Include(p => p.Stages.OrderBy(s => s.StageOrder))
            .Include(p => p.Notes)
            .Where(p => p.PatientId == patientId)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Keeps the patient with each procedure, as the mappers expect, also when read without tracking.
        foreach (var procedure in procedures)
            procedure.Patient = patient;

        var history = await Tracking(context.PatientProcedureAuditEntries, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        // Every version of every note, those of deleted procedures included.
        var noteVersions = await Tracking(context.PatientProcedureNoteVersions, forUpdate)
            .Where(v => v.PatientId == patientId)
            .OrderBy(v => v.AtUtc).ThenBy(v => v.Version).ThenBy(v => v.Id)
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

        // Notifications point at procedures, including ones since deleted, which only the audit still names, or at
        // appointments.
        var procedureIds = procedures.Select(p => p.Id)
            .Concat(history.Select(e => e.PatientProcedureId))
            .Distinct()
            .ToList();
        var appointmentIds = appointments.Select(a => a.Id)
            .Concat(appointmentHistory.Select(e => e.AppointmentId))
            .Distinct()
            .ToList();
        var notifications = await Tracking(context.Notifications, forUpdate)
            .Where(n => (n.ProcedureId != null && procedureIds.Contains(n.ProcedureId.Value))
                        || (n.AppointmentId != null && appointmentIds.Contains(n.AppointmentId.Value)))
            .OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            .ToListAsync(ct);

        // Every file, deleted ones too: their records are kept, with their names, until the bytes are purged.
        var attachments = await Tracking(context.Attachments, forUpdate)
            .Where(a => a.PatientId == patientId)
            .OrderBy(a => a.UploadedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);

        return new PatientRecord
        {
            Patient = patient,
            Procedures = procedures,
            ProcedureHistory = history,
            NoteVersions = noteVersions,
            Activity = activity,
            Appointments = appointments,
            AppointmentHistory = appointmentHistory,
            Notifications = notifications,
            Attachments = attachments
        };
    }

    public async Task SaveAsync(ActivityEntry activity, IReadOnlyCollection<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        context.PatientProcedureAuditEntries.AddRange(audit);
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
