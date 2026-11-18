using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientRepository(MedAppDbContext context) : IPatientRepository
{
    public async Task AddAsync(Patient patient, ActivityEntry activity, CancellationToken cancellationToken)
    {
        context.Patients.Add(patient);
        context.ActivityEntries.Add(activity);
        await SaveAsync(cancellationToken);
    }

    public async Task<bool> NhsNumberInUseAsync(string nhsNumber, Guid? exceptPatientId, CancellationToken cancellationToken) =>
        await context.Patients.AnyAsync(
            p => p.NhsNumber == nhsNumber && (exceptPatientId == null || p.Id != exceptPatientId),
            cancellationToken);

    public async Task<Patient?> GetByNhsNumberAsync(string nhsNumber, CancellationToken cancellationToken) =>
        await context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.NhsNumber == nhsNumber, cancellationToken);

    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> CountPathwaysAsync(Guid patientId, CancellationToken cancellationToken) =>
        await context.PatientPathways.CountAsync(p => p.PatientId == patientId, cancellationToken);

    public async Task<int> CountAppointmentsAsync(Guid patientId, CancellationToken cancellationToken) =>
        await context.Appointments.CountAsync(a => a.PatientId == patientId, cancellationToken);

    public async Task<int> CountPathwaysUnderWayAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var closed = PathwayWorkflow.ClosedStatuses.ToList();
        return await context.PatientPathways.CountAsync(p => p.PatientId == patientId && !closed.Contains(p.Status), cancellationToken);
    }

    public async Task<int> CountOpenAppointmentsAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var open = AppointmentWorkflow.OpenStatuses.ToList();
        return await context.Appointments.CountAsync(a => a.PatientId == patientId && open.Contains(a.Status), cancellationToken);
    }

    public async Task<IEnumerable<Patient>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await context.Patients
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Patient>> SearchAsync(SearchPatientsQuery query, CancellationToken cancellationToken)
    {
        var matching = context.Patients
            .AsNoTracking()
            .Filter(query);

        var totalCount = await matching.CountAsync(cancellationToken);

        var items = await matching
            .Sort(query.Sort, query.Ascending)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Patient>(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.ToList();
        return await context.Patients
            .AsNoTracking()
            .Where(p => list.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, cancellationToken);
    }

    public async Task<IReadOnlyList<RetentionCandidate>> GetRetentionCandidatesAsync(DateTime changedBeforeUtc, CancellationToken cancellationToken)
    {
        var closed = PathwayWorkflow.ClosedStatuses.ToList();
        // A download is a read, not a change to the record: it mustn't move the conclusion of treatment.
        var readsOnly = RetentionSchedule.ReadOnlyAuditActions.ToList();
        var changes = context.PatientPathwayAuditEntries.Where(a => !readsOnly.Contains(a.Action));

        return await context.Patients
            .AsNoTracking()
            .Where(p => p.AnonymisedAtUtc == null && p.LastUpdated <= changedBeforeUtc)
            .Where(p => !context.PatientPathways.Any(pp =>
                pp.PatientId == p.Id && (!closed.Contains(pp.Status) || pp.LastUpdated > changedBeforeUtc)))
            // The audit trail outlives a deleted pathway, so it counts towards the conclusion of treatment too.
            .Where(p => !changes.Any(a => a.PatientId == p.Id && a.AtUtc > changedBeforeUtc))
            .Select(p => new RetentionCandidate(
                p.Id,
                p.FirstName,
                p.LastName,
                p.DateOfBirth,
                p.LastUpdated,
                context.PatientPathways.Where(pp => pp.PatientId == p.Id).Max(pp => (DateTime?)pp.LastUpdated),
                p.RetentionKeptUntil,
                p.RetentionKeptReason,
                changes.Where(a => a.PatientId == p.Id).Max(a => (DateTime?)a.AtUtc)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Saves a patient read earlier (without tracking), and its activity entry. The row version read with it is the
    /// concurrency check: if someone else saved the patient since, nothing is saved and it's a 409.
    /// </summary>
    public async Task UpdateAsync(Patient patient, ActivityEntry activity, CancellationToken cancellationToken)
    {
        context.Patients.Update(patient);
        context.ActivityEntries.Add(activity);
        await SaveAsync(cancellationToken);
    }

    /// <summary>
    /// Saves, turning a concurrency conflict (the row version changed since it was read) into its 409
    /// (<see cref="ConcurrencyExtensions.SaveChangesOrConflictAsync"/>), and a clash on the NHS number's unique index
    /// (two saves racing past the handler's check) into the same 409 the check gives.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesOrConflictAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(NhsNumberIndex))
        {
            throw new ConflictException(NhsNumberRules.InUse);
        }
    }

    /// <summary>The unique index on the NHS number, named as EF names it.</summary>
    private const string NhsNumberIndex = "IX_Patients_NhsNumber";

    public async Task DeleteAsync(Guid id, ActivityEntry activity, CancellationToken cancellationToken)
    {
        var patient = await context.Patients.FindAsync([id], cancellationToken);

        if (patient is null)
        {
            return;
        }

        context.Patients.Remove(patient);
        context.ActivityEntries.Add(activity);
        await context.SaveChangesOrConflictAsync(cancellationToken);
    }
}
