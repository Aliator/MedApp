using MedApp.Application.Common.Paging;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Queries.SearchSpecimens;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.Infrastructure.Data.Configurations.Specimens;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Specimens and their setup lists. Changes are made to a tracked specimen (<see cref="GetForUpdateAsync"/>) and saved
/// with their audit entries in one <c>SaveChangesAsync</c>, which refuses a save made from a copy read before someone
/// else's (409). Types and labs are read tracked, so a specimen loaded with its own can be given another without two
/// copies of one row being tracked.
/// </summary>
public sealed class SpecimenRepository(MedAppDbContext context) : ISpecimenRepository
{
    public async Task<Specimen?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Specimens.AsNoTracking()).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Specimen?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Specimens).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Specimen?> GetByNumberAsync(string number, CancellationToken ct) =>
        await WithDetails(context.Specimens.AsNoTracking()).FirstOrDefaultAsync(s => s.Number == number, ct);

    public async Task<long> NextNumberAsync(CancellationToken ct)
    {
        // Not composed (SQL Server allows NEXT VALUE FOR only at the top level of a SELECT): read as a list of one.
        var values = await context.Database
            .SqlQueryRaw<long>($"SELECT NEXT VALUE FOR [{SpecimenConfiguration.NumberSequence}] AS [Value]")
            .ToListAsync(ct);
        return values.Single();
    }

    public async Task AddAsync(Specimen specimen, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.Specimens.Add(specimen);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    public async Task SaveReportAsync(Attachment report, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.Attachments.Add(report);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    private Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(SpecimenWorkflow.ChangedMeanwhileMessage, ct);

    public async Task<IReadOnlyList<Specimen>> GetForStageAsync(Guid stageId, CancellationToken ct) =>
        await context.Specimens
            .AsNoTracking()
            .Include(s => s.SpecimenType)
            .Include(s => s.Laboratory)
            .Where(s => s.StageId == stageId)
            .OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Specimen>> GetOpenForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Specimens
            .AsNoTracking()
            .Include(s => s.SpecimenType)
            .Where(s => s.PathwayId == pathwayId
                        && (s.Status == SpecimenStatus.Taken || s.Status == SpecimenStatus.Sent || s.Status == SpecimenStatus.Received))
            .OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<PagedResult<Specimen>> SearchAsync(SearchSpecimensQuery query, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.Specimens.AsNoTracking().Filter(query, nowUtc);
        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.ParsedView)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Include(s => s.Patient)
            .Include(s => s.SpecimenType)
            .Include(s => s.Laboratory)
            .ToListAsync(ct);

        return new PagedResult<Specimen>(page, totalCount);
    }

    public async Task<bool> IsReportAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Specimens.AnyAsync(s => s.ReportAttachmentId == attachmentId, ct);

    public async Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Specimens.CountAsync(s => s.PathwayId == pathwayId, ct);

    public async Task<IReadOnlyList<Specimen>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await context.Specimens.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<SpecimenType>> GetTypesAsync(bool includeArchived, CancellationToken ct) =>
        await context.SpecimenTypes
            .AsNoTracking()
            .Where(t => includeArchived || !t.IsArchived)
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<SpecimenType?> GetTypeAsync(Guid id, CancellationToken ct) =>
        await context.SpecimenTypes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<bool> TypeNameExistsAsync(string name, Guid? exceptId, CancellationToken ct) =>
        await context.SpecimenTypes.AnyAsync(t => t.Name == name && t.Id != exceptId, ct);

    public async Task<IReadOnlySet<Guid>> GetTypesInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await context.Specimens
            .Where(s => ids.Contains(s.SpecimenTypeId))
            .Select(s => s.SpecimenTypeId)
            .Distinct()
            .ToListAsync(ct))
        .ToHashSet();

    public async Task AddTypeAsync(SpecimenType type, CancellationToken ct)
    {
        context.SpecimenTypes.Add(type);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateTypeAsync(SpecimenType type, CancellationToken ct) => await context.SaveChangesAsync(ct);

    public async Task DeleteTypeAsync(Guid id, CancellationToken ct) =>
        await context.SpecimenTypes.Where(t => t.Id == id).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Laboratory>> GetLaboratoriesAsync(bool includeArchived, CancellationToken ct) =>
        await context.Laboratories
            .AsNoTracking()
            .Where(l => includeArchived || !l.IsArchived)
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<Laboratory?> GetLaboratoryAsync(Guid id, CancellationToken ct) =>
        await context.Laboratories.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<bool> LaboratoryNameExistsAsync(string name, Guid? exceptId, CancellationToken ct) =>
        await context.Laboratories.AnyAsync(l => l.Name == name && l.Id != exceptId, ct);

    public async Task<IReadOnlySet<Guid>> GetLaboratoriesInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await context.Specimens
            .Where(s => ids.Contains(s.LaboratoryId))
            .Select(s => s.LaboratoryId)
            .Distinct()
            .ToListAsync(ct))
        .ToHashSet();

    public async Task AddLaboratoryAsync(Laboratory laboratory, CancellationToken ct)
    {
        context.Laboratories.Add(laboratory);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateLaboratoryAsync(Laboratory laboratory, CancellationToken ct) => await context.SaveChangesAsync(ct);

    public async Task DeleteLaboratoryAsync(Guid id, CancellationToken ct) =>
        await context.Laboratories.Where(l => l.Id == id).ExecuteDeleteAsync(ct);

    /// <summary>A specimen with its patient, pathway (and its stages, for the stage's name and the audit), type and lab.</summary>
    private static IQueryable<Specimen> WithDetails(IQueryable<Specimen> specimens) =>
        specimens
            .Include(s => s.Patient)
            .Include(s => s.Pathway).ThenInclude(p => p.Stages)
            .Include(s => s.SpecimenType)
            .Include(s => s.Laboratory);
}
