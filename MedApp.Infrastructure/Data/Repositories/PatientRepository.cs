using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using Microsoft.Data.SqlClient;
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
        await SaveAsync(cancellationToken);
    }

    /// <summary>
    /// Saves, turning a clash on the NHS number's unique index (two saves racing past the handler's check) into the
    /// same 409 the check gives.
    /// </summary>
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 } sql
                                          && sql.Message.Contains(NhsNumberIndex, StringComparison.Ordinal))
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
        await context.SaveChangesAsync(cancellationToken);
    }
}
