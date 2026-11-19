using MedApp.Application.Common.Paging;
using MedApp.Application.Hospitals.Repositories;
using MedApp.Application.Hospitals.Requests;
using MedApp.Domain.Activity;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Hospitals and their requests (MC18). Shares the request's DbContext with the lab case repository, which saves a
/// registration with its case.
/// </summary>
public sealed class HospitalRepository(MedAppDbContext context) : IHospitalRepository
{
    public async Task<IReadOnlyList<Hospital>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.Hospitals
            .AsNoTracking()
            .Where(h => includeArchived || !h.IsArchived)
            .OrderBy(h => h.Name).ThenBy(h => h.Id)
            .ToListAsync(ct);

    public async Task<Hospital?> GetAsync(Guid id, CancellationToken ct) =>
        await context.Hospitals.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct) =>
        await context.Hospitals.AnyAsync(h => h.Name == name && h.Id != exceptId, ct);

    public async Task<bool> OdsCodeExistsAsync(string odsCode, Guid? exceptId, CancellationToken ct) =>
        await context.Hospitals.AnyAsync(h => h.OdsCode == odsCode && h.Id != exceptId, ct);

    public async Task<IReadOnlySet<Guid>> GetInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return (await context.HospitalRequests
                .Where(r => list.Contains(r.HospitalId))
                .Select(r => r.HospitalId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();
    }

    public async Task AddAsync(Hospital hospital, CancellationToken ct)
    {
        context.Hospitals.Add(hospital);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Hospital hospital, CancellationToken ct) => await context.SaveChangesAsync(ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct) =>
        await context.Hospitals.Where(h => h.Id == id).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Patient>> FindPatientMatchesAsync(
        string? nhsNumber, string? lastName, DateOnly? dateOfBirth, CancellationToken ct)
    {
        var byDetails = lastName is not null && dateOfBirth is not null;
        var name = lastName?.ToLower();
        return await context.Patients
            .AsNoTracking()
            .Where(p => p.AnonymisedAtUtc == null)
            .Where(p => (nhsNumber != null && p.NhsNumber == nhsNumber)
                        || (byDetails && p.LastName.ToLower() == name && p.DateOfBirth == dateOfBirth))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<Patient?> GetPatientForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> NhsNumberInUseAsync(string nhsNumber, CancellationToken ct) =>
        await context.Patients.AnyAsync(p => p.NhsNumber == nhsNumber, ct);

    public async Task<HospitalRequest?> GetRequestAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.HospitalRequests.AsNoTracking()).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, LabCaseStatus>> GetCaseStatusesAsync(
        IReadOnlyCollection<Guid> labCaseIds, CancellationToken ct)
    {
        var ids = labCaseIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, LabCaseStatus>();
        return await context.LabCases
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Status, ct);
    }

    public async Task<PagedResult<HospitalRequest>> SearchAsync(SearchHospitalRequestsQuery query, CancellationToken ct)
    {
        var filtered = context.HospitalRequests.AsNoTracking().Filter(query);
        var totalCount = await filtered.CountAsync(ct);
        var page = await WithDetails(filtered
                .Sort(query.Sort, query.Ascending)
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            .ToListAsync(ct);
        return new PagedResult<HospitalRequest>(page, totalCount);
    }

    public void TrackRegistration(
        HospitalRequest request,
        Specimen sample,
        Patient? newPatient,
        ActivityEntry? patientCreated,
        IEnumerable<PatientPathwayAuditEntry> audit)
    {
        if (newPatient is not null)
            context.Patients.Add(newPatient);
        if (patientCreated is not null)
            context.ActivityEntries.Add(patientCreated);
        context.HospitalRequests.Add(request);
        context.Specimens.Add(sample);
        context.PatientPathwayAuditEntries.AddRange(audit);
    }

    private static IQueryable<HospitalRequest> WithDetails(IQueryable<HospitalRequest> requests) =>
        requests
            .Include(r => r.Hospital)
            .Include(r => r.Patient)
            .Include(r => r.SpecimenType)
            .Include(r => r.LabTest);
}
