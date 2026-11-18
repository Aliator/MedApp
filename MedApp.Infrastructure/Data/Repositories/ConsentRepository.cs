using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Consent records and consent forms. Changes are made to a tracked record (<see cref="GetForUpdateAsync"/>) and saved
/// with their audit entries, and a scan soft-deleted with them, in one <c>SaveChangesAsync</c>, which refuses a save
/// made from a copy read before someone else's (409).
/// </summary>
public sealed class ConsentRepository(MedAppDbContext context) : IConsentRepository
{
    public async Task<ConsentRecord?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.ConsentRecords
            .Include(c => c.Patient)
            .Include(c => c.Pathway).ThenInclude(p => p.Stages)
            .Include(c => c.ConsentForm)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ConsentRecord>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.ConsentRecords
            .AsNoTracking()
            .Where(c => c.PathwayId == pathwayId)
            .OrderBy(c => c.RecordedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ConsentRecord>> GetForStageAsync(Guid stageId, CancellationToken ct) =>
        await context.ConsentRecords
            .AsNoTracking()
            .Where(c => c.StageId == stageId)
            .OrderBy(c => c.RecordedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public async Task AddAsync(ConsentRecord record, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.ConsentRecords.Add(record);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveAsync(ct);
    }

    private Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(ConsentWorkflow.ChangedMeanwhileMessage, ct);

    public async Task<Attachment?> GetAttachmentForUpdateAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);

    public async Task<bool> IsScanAsync(Guid attachmentId, CancellationToken ct) =>
        await context.ConsentRecords.AnyAsync(c => c.ScanAttachmentId == attachmentId, ct);

    public async Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.ConsentRecords.CountAsync(c => c.PathwayId == pathwayId, ct);

    public async Task<IReadOnlyList<ConsentRecord>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await context.ConsentRecords.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<ConsentForm>> GetFormsAsync(bool includeArchived, CancellationToken ct) =>
        await context.ConsentForms
            .AsNoTracking()
            .Where(f => includeArchived || !f.IsArchived)
            .OrderBy(f => f.Name).ThenBy(f => f.FormVersion).ThenBy(f => f.Id)
            .ToListAsync(ct);

    public async Task<ConsentForm?> GetFormAsync(Guid id, CancellationToken ct) =>
        await context.ConsentForms.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<bool> FormExistsAsync(string name, string formVersion, Guid? exceptId, CancellationToken ct) =>
        await context.ConsentForms.AnyAsync(
            f => f.Name == name && f.FormVersion == formVersion && (exceptId == null || f.Id != exceptId), ct);

    public async Task<IReadOnlySet<Guid>> GetFormsInUseAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        (await context.ConsentRecords
            .Where(c => ids.Contains(c.ConsentFormId))
            .Select(c => c.ConsentFormId)
            .Distinct()
            .ToListAsync(ct))
        .ToHashSet();

    public async Task AddFormAsync(ConsentForm form, CancellationToken ct)
    {
        context.ConsentForms.Add(form);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateFormAsync(ConsentForm form, CancellationToken ct) => await context.SaveChangesAsync(ct);

    public async Task DeleteFormAsync(Guid id, CancellationToken ct) =>
        await context.ConsentForms.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
}
