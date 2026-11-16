using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Attachments.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class AttachmentRepository(MedAppDbContext context) : IAttachmentRepository
{
    public async Task AddAsync(Attachment attachment, PatientProcedureAuditEntry audit, CancellationToken ct)
    {
        context.Attachments.Add(attachment);
        context.PatientProcedureAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Attachment?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.Attachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task SaveAsync(PatientProcedureAuditEntry audit, CancellationToken ct)
    {
        context.PatientProcedureAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task AddAuditAsync(PatientProcedureAuditEntry audit, CancellationToken ct)
    {
        context.PatientProcedureAuditEntries.Add(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AttachmentRow> Rows, int TotalCount)> ListAsync(GetAttachmentsQuery query, CancellationToken ct)
    {
        var filtered = AttachmentSearch.Filter(context.Attachments.AsNoTracking(), query);
        var totalCount = await filtered.CountAsync(ct);
        var rows = await AttachmentSearch
            .Newest(filtered, context.PatientProcedures.AsNoTracking(), context.PatientProcedureStages.AsNoTracking())
            .ToListAsync(ct);

        return (rows, totalCount);
    }

    public async Task<Guid?> GetStageProcedureIdAsync(Guid stageId, CancellationToken ct) =>
        await context.PatientProcedureStages
            .AsNoTracking()
            .Where(s => s.Id == stageId)
            .Select(s => (Guid?)s.PatientProcedureId)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountForPatientAsync(Guid patientId, CancellationToken ct) =>
        context.Attachments.CountAsync(a => a.PatientId == patientId && a.DeletedAtUtc == null, ct);

    public Task<int> CountForProcedureAsync(Guid procedureId, CancellationToken ct) =>
        context.Attachments.CountAsync(a => a.ProcedureId == procedureId && a.DeletedAtUtc == null, ct);
}
