using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Application.Referrals.Common;
using MedApp.Infrastructure.Data.Configurations.Appointments;
using MedApp.Infrastructure.Data.Configurations.Referrals;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Pathways. Changes are made to a tracked pathway (<see cref="GetForUpdateAsync"/>) and saved with their audit
/// entries in one <c>SaveChangesAsync</c>, so EF works out which stages and assignments changed. A
/// <see cref="PathwayChange"/> also brings the appointments it asked for and the history of those it changed;
/// appointments loaded for update by <c>AppointmentRepository</c> share this DbContext, so the same save saves them.
/// </summary>
public sealed class PatientPathwayRepository(MedAppDbContext context) : IPatientPathwayRepository
{
    public async Task AddAsync(PatientPathway pathway, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathways.Add(pathway);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task AddAsync(PathwayChange change, CancellationToken ct)
    {
        context.PatientPathways.Add(change.Pathway);
        AddChange(change);
        await SaveChangesAsync(ct, ConflictMessage(change));
    }

    public async Task<PatientPathway?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientPathways.AsNoTracking())
            .Include(x => x.Notes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientPathway?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientPathways)
            .Include(x => x.Notes)
            // The referral it was accepted from, with its team's targets and its pauses: a change may move its
            // waiting-time clock, saved in the same save (PathwayChange).
            .Include(x => x.Referral!).ThenInclude(r => r.Team)
            .Include(x => x.Referral!).ThenInclude(r => r.Pauses)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PatientPathway>> GetByPatientAsync(Guid patientId, CancellationToken ct) =>
        await context.PatientPathways
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .ToListAsync(ct);

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task SaveAsync(PathwayChange change, CancellationToken ct)
    {
        AddChange(change);
        await SaveChangesAsync(ct, ConflictMessage(change));
    }

    /// <summary>
    /// What a concurrency conflict means for this change: for a deletion, the note or the request to delete it (saved
    /// with it) changed since they were loaded; for an add or edit, a note's row version changed (it was edited
    /// meanwhile); otherwise one of the pathway's appointments.
    /// </summary>
    private static string ConflictMessage(PathwayChange change) =>
        change.DeletedNotes.Count > 0 ? PathwayNotes.DeletedMeanwhileMessage
        : change.NoteVersions.Count > 0 ? PathwayNotes.ChangedMeanwhileMessage
        : change.ReferralEvents.Count > 0 ? PathwayClockConflictMessage
        : AppointmentWorkflow.PathwayAppointmentChangedMeanwhileMessage;

    /// <summary>A change that moved the referral's clock lost a race with another change to the referral or the pathway's appointments.</summary>
    public const string PathwayClockConflictMessage =
        "Someone else changed this pathway's referral or its appointments while you had it open. Reload it, then try again.";

    public async Task DeleteAsync(PatientPathway pathway, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        // Its appointments are kept, detached from it (their history keeps the pathway), in the same save, so the
        // stages can go.
        var appointments = await context.Appointments
            .Where(a => a.PathwayId == pathway.Id)
            .ToListAsync(ct);
        foreach (var appointment in appointments)
        {
            appointment.Pathway = null;
            appointment.PathwayId = null;
            appointment.Stage = null;
            appointment.StageId = null;
        }

        // A referral accepted into it stays, with its history, no longer pointing at it.
        var referrals = await context.Referrals
            .Where(r => r.PathwayId == pathway.Id)
            .ToListAsync(ct);
        foreach (var referral in referrals)
        {
            referral.Pathway = null;
            referral.PathwayId = null;
        }

        // Its MDT items go with it (their history stays in the audit log), with who was present (cascade).
        context.MdtItems.RemoveRange(await context.MdtItems.Where(i => i.PathwayId == pathway.Id).ToListAsync(ct));

        // Stages and assignments cascade; the audit entries have no foreign key, so they stay.
        context.PatientPathways.Remove(pathway);
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task<PagedResult<PatientPathway>> SearchAsync(SearchPatientPathwaysQuery query, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.PatientPathways.AsNoTracking().Filter(query, nowUtc);

        var totalCount = await filtered.CountAsync(ct);

        var page = await WithDetails(filtered
                .Sort(query.Sort, query.Ascending, DueDates.ToUkDate(nowUtc))
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            // Its referral's clock, for the Wait column.
            .Include(x => x.Referral)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<PatientPathway>(page, totalCount);
    }

    public async Task<IReadOnlyList<PathwayLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList();
        return await context.PatientPathways
            .AsNoTracking()
            .Where(p => list.Contains(p.Id))
            .Select(p => new PathwayLabel(p.Id, p.Title, p.Patient.FirstName, p.Patient.LastName))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Adds a change's audit entries, the appointments it asked for, the history entries it wrote, and the notes it added
    /// with their versions (and the versions of those it edited).
    /// </summary>
    private void AddChange(PathwayChange change)
    {
        context.PatientPathwayAuditEntries.AddRange(change.AuditEntries);
        context.Appointments.AddRange(change.NewAppointments);
        context.AppointmentEvents.AddRange(change.AppointmentEvents);
        context.PatientPathwayNotes.AddRange(change.NewNotes);
        context.PatientPathwayNoteVersions.AddRange(change.NoteVersions);
        context.ReferralEvents.AddRange(change.ReferralEvents);
        context.ReferralClockPauses.AddRange(change.NewPauses);
    }

    /// <summary>
    /// Saves. A pathway change can change its appointments too (cancelling a Requested visit, detaching them) or
    /// ask for one, so: if someone else saved one of those appointments since it was loaded (its row version), or gave
    /// the stage its open visit first (the one-open-per-stage index), nothing is saved and it's a 409. Trying again
    /// starts from the appointments as they now are.
    /// </summary>
    private async Task SaveChangesAsync(CancellationToken ct, string? conflictMessage = null)
    {
        try
        {
            await context.SaveChangesOrConflictAsync(
                conflictMessage ?? AppointmentWorkflow.PathwayAppointmentChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(AppointmentConfiguration.OpenPerStageIndex))
        {
            throw new ConflictException(AppointmentWorkflow.StageVisitAddedMeanwhileMessage);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(ReferralClockPauseConfiguration.OpenPerReferralIndex))
        {
            throw new ConflictException(WaitingClock.PausedMeanwhileMessage);
        }
    }

    private static IQueryable<PatientPathway> WithDetails(IQueryable<PatientPathway> pathways) =>
        pathways
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder));
}
