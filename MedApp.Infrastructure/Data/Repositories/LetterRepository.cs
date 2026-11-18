using MedApp.Application.Common.Paging;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Letters, with their recipients. Shares the request's DbContext with the other repositories, so a letter, its PDF's
/// attachment record and their audit entries are saved together.
/// </summary>
public sealed class LetterRepository(MedAppDbContext context) : ILetterRepository
{
    public async Task<Letter?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Letters.AsNoTracking().Include(l => l.Recipients).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<Letter?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await context.Letters.Include(l => l.Recipients).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task AddAsync(Letter letter, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.Letters.Add(letter);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await context.SaveChangesOrConflictAsync(LetterWorkflow.ChangedMeanwhileMessage, ct);
    }

    public void ReplaceRecipients(Letter letter, IReadOnlyList<LetterRecipient> recipients)
    {
        var old = letter.Recipients.ToList();
        letter.Recipients.Clear();
        context.LetterRecipients.RemoveRange(old);
        foreach (var recipient in recipients)
        {
            // Added explicitly: a new recipient's ID is already set, which EF would otherwise take for an existing row.
            // Adding it also puts it in the letter's list (EF's fix-up), so it's added there only if it isn't.
            context.LetterRecipients.Add(recipient);
            if (!letter.Recipients.Contains(recipient))
                letter.Recipients.Add(recipient);
        }
    }

    public async Task SaveApprovalAsync(Attachment pdf, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.Attachments.Add(pdf);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await context.SaveChangesOrConflictAsync(LetterWorkflow.ChangedMeanwhileMessage, ct);
    }

    public async Task<PagedResult<LetterRow>> SearchAsync(SearchLettersQuery query, CancellationToken ct)
    {
        var letters = LetterSearch.Apply(context.Letters.AsNoTracking(), query, context.Patients.AsNoTracking());
        var total = await letters.CountAsync(ct);

        var page = await letters
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToListAsync(ct);

        // The page's recipients (for the counts the list shows), patients' names and pathways' titles, each in one query.
        var ids = page.Select(l => l.Id).ToList();
        var recipients = await context.LetterRecipients.AsNoTracking()
            .Where(r => ids.Contains(r.LetterId))
            .ToListAsync(ct);
        var patientIds = page.Select(l => l.PatientId).Distinct().ToList();
        var patients = await context.Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToDictionaryAsync(p => p.Id, ct);
        var pathwayIds = page.Select(l => l.PathwayId).OfType<Guid>().Distinct().ToList();
        var titles = await context.PatientPathways.AsNoTracking()
            .Where(p => pathwayIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title })
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        var rows = page.Select(letter =>
        {
            letter.Recipients = recipients.Where(r => r.LetterId == letter.Id).OrderBy(r => r.Position).ToList();
            var patient = patients.GetValueOrDefault(letter.PatientId);
            return new LetterRow(
                letter,
                patient?.FirstName ?? string.Empty,
                patient?.LastName ?? string.Empty,
                letter.PathwayId is { } pathwayId ? titles.GetValueOrDefault(pathwayId) : null);
        }).ToList();

        return new PagedResult<LetterRow>(rows, total);
    }

    public async Task<(int ToApprove, int ToSend)> CountAsync(Guid viewerId, CancellationToken ct)
    {
        var toApprove = await context.Letters.CountAsync(
            l => l.Status == LetterStatus.AwaitingApproval && l.ApproverUserId == viewerId, ct);
        var toSend = await LetterSearch.ToSend(context.Letters).CountAsync(ct);
        return (toApprove, toSend);
    }

    public async Task<IReadOnlyList<Letter>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Letters.AsNoTracking()
            .Include(l => l.Recipients)
            .Where(l => l.PathwayId == pathwayId)
            .OrderBy(l => l.CreatedAt).ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Letter>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0
            ? []
            : await context.Letters.AsNoTracking().Include(l => l.Recipients).Where(l => ids.Contains(l.Id)).ToListAsync(ct);

    public async Task<Guid?> GetCorrectionIdAsync(Guid letterId, CancellationToken ct) =>
        await context.Letters
            .Where(l => l.ReplacesLetterId == letterId)
            .OrderBy(l => l.CreatedAt)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsPdfAsync(Guid attachmentId, CancellationToken ct) =>
        await context.Letters.AnyAsync(l => l.PdfAttachmentId == attachmentId, ct);

    public async Task<int> CountForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Letters.CountAsync(l => l.PathwayId == pathwayId, ct);

    public async Task<DateTime?> GetLastAttendedAsync(Guid pathwayId, CancellationToken ct) =>
        await context.Appointments
            .Where(a => a.PathwayId == pathwayId && a.Status == AppointmentStatus.Attended && a.StartUtc != null)
            .MaxAsync(a => a.StartUtc, ct);
}
