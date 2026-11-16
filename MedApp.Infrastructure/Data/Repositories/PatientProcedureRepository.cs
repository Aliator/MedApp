using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.Infrastructure.Data.Configurations.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Procedures. Changes are made to a tracked procedure (<see cref="GetForUpdateAsync"/>) and saved with their audit
/// entries in one <c>SaveChangesAsync</c>, so EF works out which stages and assignments changed. A
/// <see cref="ProcedureChange"/> also brings the appointments it asked for and the history of those it changed;
/// appointments loaded for update by <c>AppointmentRepository</c> share this DbContext, so the same save saves them.
/// </summary>
public sealed class PatientProcedureRepository(MedAppDbContext context) : IPatientProcedureRepository
{
    public async Task AddAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        context.PatientProcedures.Add(procedure);
        context.PatientProcedureAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task AddAsync(ProcedureChange change, CancellationToken ct)
    {
        context.PatientProcedures.Add(change.Procedure);
        AddChange(change);
        await SaveChangesAsync(ct);
    }

    public async Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientProcedures.AsNoTracking())
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientProcedure?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientProcedures)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PatientProcedure>> GetByPatientAsync(Guid patientId, CancellationToken ct) =>
        await context.PatientProcedures
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .ToListAsync(ct);

    public async Task SaveAsync(IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        context.PatientProcedureAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task SaveAsync(ProcedureChange change, CancellationToken ct)
    {
        AddChange(change);
        await SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PatientProcedure procedure, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken ct)
    {
        // Its appointments are kept, detached from it (their history keeps the procedure), in the same save, so the
        // stages can go.
        var appointments = await context.Appointments
            .Where(a => a.ProcedureId == procedure.Id)
            .ToListAsync(ct);
        foreach (var appointment in appointments)
        {
            appointment.Procedure = null;
            appointment.ProcedureId = null;
            appointment.Stage = null;
            appointment.StageId = null;
        }

        // Stages and assignments cascade; the audit entries have no foreign key, so they stay.
        context.PatientProcedures.Remove(procedure);
        context.PatientProcedureAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task<PagedResult<PatientProcedure>> SearchAsync(SearchPatientProceduresQuery query, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.PatientProcedures.AsNoTracking().Filter(query, nowUtc);

        var totalCount = await filtered.CountAsync(ct);

        var page = await WithDetails(filtered
                .Sort(query.Sort, query.Ascending)
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<PatientProcedure>(page, totalCount);
    }

    public async Task<IReadOnlyList<ProcedureLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return await context.PatientProcedures
            .AsNoTracking()
            .Where(p => list.Contains(p.Id))
            .Select(p => new ProcedureLabel(p.Id, p.Title, p.Patient.FirstName, p.Patient.LastName))
            .ToListAsync(ct);
    }

    /// <summary>Adds a change's audit entries, the appointments it asked for and the history entries it wrote.</summary>
    private void AddChange(ProcedureChange change)
    {
        context.PatientProcedureAuditEntries.AddRange(change.AuditEntries);
        context.Appointments.AddRange(change.NewAppointments);
        context.AppointmentEvents.AddRange(change.AppointmentEvents);
    }

    /// <summary>
    /// Saves. A procedure change can change its appointments too (cancelling a Requested visit, detaching them) or
    /// ask for one, so: if someone else saved one of those appointments since it was loaded (its row version), or gave
    /// the stage its open visit first (the one-open-per-stage index), nothing is saved and it's a 409. Trying again
    /// starts from the appointments as they now are.
    /// </summary>
    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesOrConflictAsync(AppointmentWorkflow.ProcedureAppointmentChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(AppointmentConfiguration.OpenPerStageIndex))
        {
            throw new ConflictException(AppointmentWorkflow.StageVisitAddedMeanwhileMessage);
        }
    }

    private static IQueryable<PatientProcedure> WithDetails(IQueryable<PatientProcedure> procedures) =>
        procedures
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder));
}
