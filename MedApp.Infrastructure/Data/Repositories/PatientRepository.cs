using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientRepository(MedAppDbContext context) : IPatientRepository
{
    public async Task AddAsync(Patient patient, CancellationToken cancellationToken)
    {
        context.Patients.Add(patient);
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

    public async Task UpdateAsync(Patient patient, CancellationToken cancellationToken)
    {
        context.Patients.Update(patient);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var patient = await context.Patients.FindAsync([id], cancellationToken);

        if (patient is null)
        {
            return;
        }

        context.Patients.Remove(patient);
        await context.SaveChangesAsync(cancellationToken);
    }
}