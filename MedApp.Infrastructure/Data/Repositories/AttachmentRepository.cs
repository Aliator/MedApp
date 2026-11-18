using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Attachments.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class AttachmentRepository(MedAppDbContext context) : IAttachmentRepository
{
    public async Task AddAsync(Attachment attachment, PatientPathwayAuditEntry audit, CancellationToken ct)
    {
        context.Attachments.Add(attachment);
        context.PatientPathwayAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Attachment?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task SaveAsync(PatientPathwayAuditEntry audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddAuditAsync(PatientPathwayAuditEntry audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AttachmentRow> Rows, int TotalCount)> ListAsync(GetAttachmentsQuery query, CancellationToken ct)
    {
        var filtered = AttachmentSearch.Filter(context.Attachments.AsNoTracking(), query);
        var totalCount = await filtered.CountAsync(ct);
        var rows = await AttachmentSearch
            .Newest(filtered, context.PatientPathways.AsNoTracking(), context.PatientPathwayStages.AsNoTracking())
            .ToListAsync(ct);

        return (rows, totalCount);
    }

    public async Task<Referral?> GetReferralAsync(Guid referralId, CancellationToken ct) =>
        await context.Referrals
            .AsNoTracking()
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == referralId, ct);

    public async Task<Guid?> GetStagePathwayIdAsync(Guid stageId, CancellationToken ct) =>
        await context.PatientPathwayStages
            .AsNoTracking()
            .Where(s => s.Id == stageId)
            .Select(s => (Guid?)s.PatientPathwayId)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct) =>
        context.Attachments.CountAsync(a => a.PatientId == patientId && a.DeletedAtUtc == null, ct);

    public Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        context.Attachments.CountAsync(a => a.PathwayId == pathwayId && a.DeletedAtUtc == null, ct);
}
