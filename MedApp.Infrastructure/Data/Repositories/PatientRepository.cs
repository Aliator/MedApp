using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
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
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> CountProceduresAsync(Guid patientId, CancellationToken cancellationToken) =>
        await context.PatientProcedures.CountAsync(p => p.PatientId == patientId, cancellationToken);

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
        var closed = ProcedureWorkflow.ClosedStatuses.ToList();

        return await context.Patients
            .AsNoTracking()
            .Where(p => p.AnonymisedAtUtc == null && p.LastUpdated <= changedBeforeUtc)
            .Where(p => !context.PatientProcedures.Any(pp =>
                pp.PatientId == p.Id && (!closed.Contains(pp.Status) || pp.LastUpdated > changedBeforeUtc)))
            .Select(p => new RetentionCandidate(
                p.Id,
                p.FirstName,
                p.LastName,
                p.DateOfBirth,
                p.LastUpdated,
                context.PatientProcedures.Where(pp => pp.PatientId == p.Id).Max(pp => (DateTime?)pp.LastUpdated),
                p.RetentionKeptUntil,
                p.RetentionKeptReason))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Patient patient, ActivityEntry activity, CancellationToken cancellationToken)
    {
        context.Patients.Update(patient);
        context.ActivityEntries.Add(activity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, ActivityEntry activity, CancellationToken cancellationToken)
    {
        var patient = await context.Patients.FindAsync([id], cancellationToken);

        if (patient is null)
        {
            return;
        }

        context.Patients.Remove(patient);
        context.ActivityEntries.Add(activity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
