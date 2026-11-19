using MedApp.Application.Appointments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.Application.Referrals.Common;
using MedApp.Infrastructure.Data.Configurations.Appointments;
using MedApp.Infrastructure.Data.Configurations.Referrals;
using MedApp.Application.Lab.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Pathways. Changes are made to a tracked pathway (<see cref="GetForUpdateAsync"/>) and saved with their audit
/// entries in one <c>SaveChangesAsync</c>, so EF works out which stages and assignments changed. A
/// <see cref="PathwayChange"/> also brings the appointments it asked for and the history of those it changed;
/// appointments loaded for update by <c>AppointmentRepository</c> share this DbContext, so the same save saves them.
/// </summary>
public sealed class PatientPathwayRepository(MedAppDbContext context, IOptions<SpecimenOptions> specimenOptions)
    : IPatientPathwayRepository
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
        await AddChangeAsync(change, ct);
        await SaveChangesAsync(ct, ConflictMessage(change));
    }

    public async Task<PatientPathway?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientPathways.AsNoTracking())
            .Include(x => x.Notes)
            // A lab stage's cases (MC16), for its labCase fields and the gate.
            .Include(x => x.Stages).ThenInclude(s => s.LabCases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<PatientPathway?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.PatientPathways)
            .Include(x => x.Notes)
            .Include(x => x.Stages).ThenInclude(s => s.LabCases)
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

    public async Task<IReadOnlyList<PatientPathway>> GetByPatientAsync(Guid patientId, TeamScope scope, CancellationToken ct) =>
        await context.PatientPathways
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Where(TeamAccess.Pathways(scope, context.PathwayAccess))
            .ToListAsync(ct);

    public async Task SaveAsync(IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(audit);
        await SaveChangesAsync(ct);
    }

    public async Task SaveAsync(PathwayChange change, CancellationToken ct)
    {
        await AddChangeAsync(change, ct);
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

    public async Task<PagedResult<PatientPathway>> SearchAsync(
        SearchPatientPathwaysQuery query, TeamScope scope, DateTime nowUtc, CancellationToken ct)
    {
        var filtered = context.PatientPathways
            .AsNoTracking()
            .Where(TeamAccess.Pathways(scope, context.PathwayAccess))
            .Filter(query, nowUtc);

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

    public async Task<IReadOnlyList<PathwayLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken ct)
    {
        var list = ids.ToList();
        return await context.PatientPathways
            .AsNoTracking()
            .Where(p => list.Contains(p.Id))
            .Where(TeamAccess.Pathways(scope, context.PathwayAccess))
            .Select(p => new PathwayLabel(p.Id, p.Title, p.Patient.FirstName, p.Patient.LastName))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Adds a change's audit entries, the appointments it asked for, the history entries it wrote, the notes it added
    /// with their versions (and the versions of those it edited), the stages, links and branches it added (MC8 to
    /// MC10b), and its grants: a team's stage owner grant only once (MC7: a team handed a pathway again keeps the grant
    /// it has; a consult's, which ends, doesn't count).
    /// </summary>
    private async Task AddChangeAsync(PathwayChange change, CancellationToken ct)
    {
        // The stages it added first: their keys are set, so they're added, not taken for existing rows.
        context.PatientPathwayStages.AddRange(change.NewStages.Where(s => context.Entry(s).State != EntityState.Added));
        context.PatientPathwayStageLinks.AddRange(change.NewLinks.Where(l => context.Entry(l).State != EntityState.Added));
        context.PathwayBranchAdditions.AddRange(change.NewBranchAdditions);
        var grants = new List<PathwayAccess>();
        foreach (var grant in change.NewGrants)
        {
            if (grant.Reason == PathwayAccessReason.StageOwner
                && (grants.Any(g => g.TeamId == grant.TeamId && g.Reason == PathwayAccessReason.StageOwner)
                    || await context.PathwayAccess.AnyAsync(a => a.PathwayId == grant.PathwayId && a.TeamId == grant.TeamId
                                                                && a.Reason != PathwayAccessReason.Consult, ct)))
                continue;

            grants.Add(grant);
        }

        context.PatientPathwayAuditEntries.AddRange(change.AuditEntries);
        await OpenLabStageCasesAsync(change, ct);
        context.Appointments.AddRange(change.NewAppointments);
        context.AppointmentEvents.AddRange(change.AppointmentEvents);
        context.PatientPathwayNotes.AddRange(change.NewNotes);
        context.PatientPathwayNoteVersions.AddRange(change.NoteVersions);
        context.ReferralEvents.AddRange(change.ReferralEvents);
        context.ReferralClockPauses.AddRange(change.NewPauses);
        context.PathwayAccess.AddRange(grants);
    }

    /// <summary>
    /// MC16: each lab stage the change made current opens its lab case, unless it has one open, waiting for its sample
    /// (the test from the samples taken for it, else chosen by the lab when the sample comes), numbered from the one
    /// sequence; saved in the same save, with its <c>LabCaseOpened</c> entry.
    /// </summary>
    private async Task OpenLabStageCasesAsync(PathwayChange change, CancellationToken ct)
    {
        if (!change.ReachedStages.Any(s => s.OwnerTeamId is not null))
            return;

        var labTeamId = await context.Teams
            .Where(t => t.Kind == Domain.Teams.TeamKind.Lab && !t.IsArchived)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
        if (labTeamId is not { } lab)
            return;

        var reached = LabStages.ReachedLabStages(change, lab);
        if (reached.Count == 0)
            return;

        var pathway = change.Pathway;
        var samples = await context.Specimens.AsNoTracking().Where(s => s.PathwayId == pathway.Id).ToListAsync(ct);
        foreach (var stage in reached)
        {
            var testId = LabStages.TestFromSamples(pathway, stage, samples, lab);
            var test = testId is { } id ? await context.LabTests.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Id == id, ct) : null;
            var sequence = await NextSpecimenNumberAsync(ct);
            var opened = LabCaseChange.Open(
                SpecimenNumbers.Format(specimenOptions.Value.NumberPrefix, change.NowUtc, sequence),
                testId, test, pathway.PatientId, lab, Domain.Specimens.SpecimenSource.Clinic, pathway.TeamId,
                null, null, pathway, stage.Id, [], change.Actor, change.NowUtc);
            stage.LabCases.Add(opened.Case);
            context.LabCases.Add(opened.Case);
            context.PatientPathwayAuditEntries.AddRange(opened.AuditEntries);
        }
    }

    private async Task<long> NextSpecimenNumberAsync(CancellationToken ct)
    {
        var values = await context.Database
            .SqlQueryRaw<long>($"SELECT NEXT VALUE FOR [{Configurations.Specimens.SpecimenConfiguration.NumberSequence}] AS [Value]")
            .ToListAsync(ct);
        return values.Single();
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
            .Include(x => x.Team)
            .Include(x => x.Assignments)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder)).ThenInclude(s => s.OwnerTeam)
            .Include(x => x.Stages.OrderBy(s => s.StageOrder)).ThenInclude(s => s.HandedOverFromTeam)
            .Include(x => x.Links)
            .Include(x => x.BranchAdditions);
}
