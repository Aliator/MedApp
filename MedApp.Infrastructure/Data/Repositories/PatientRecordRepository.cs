using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
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

        var activity = await Tracking(context.ActivityEntries, forUpdate)
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

        // Notifications point at procedures, including ones since deleted, which only the audit still names.
        var procedureIds = procedures.Select(p => p.Id)
            .Concat(history.Select(e => e.PatientProcedureId))
            .Distinct()
            .ToList();
        var notifications = await Tracking(context.Notifications, forUpdate)
            .Where(n => n.ProcedureId != null && procedureIds.Contains(n.ProcedureId.Value))
            .OrderBy(n => n.CreatedAtUtc).ThenBy(n => n.Id)
            .ToListAsync(ct);

        return new PatientRecord
        {
            Patient = patient,
            Procedures = procedures,
            ProcedureHistory = history,
            Activity = activity,
            Notifications = notifications
        };
    }

    public async Task SaveAsync(ActivityEntry activity, CancellationToken ct)
    {
        context.ActivityEntries.Add(activity);
        await context.SaveChangesAsync(ct);
    }

    private static IQueryable<T> Tracking<T>(DbSet<T> set, bool forUpdate) where T : class =>
        forUpdate ? set : set.AsNoTracking();
}
