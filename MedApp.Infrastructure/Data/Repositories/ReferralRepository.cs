using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Application.Referrals.Repositories;
using MedApp.Domain.Referrals;
using MedApp.Infrastructure.Data.Configurations.Referrals;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Referrals. Changes are made to a tracked referral (<see cref="GetForUpdateAsync"/>) and saved with their history
/// entries in one <c>SaveChangesAsync</c>, which refuses a save made from a copy read before someone else's (409).
/// </summary>
public sealed class ReferralRepository(MedAppDbContext context) : IReferralRepository
{
    public async Task<Referral?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Referrals.AsNoTracking()).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<Referral?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Referrals).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Referral referral, IEnumerable<ReferralEvent> events, CancellationToken ct)
    {
        context.Referrals.Add(referral);
        context.ReferralEvents.AddRange(events);
        await SaveAsync(ct);
    }

    public async Task SaveAsync(IEnumerable<ReferralEvent> events, CancellationToken ct)
    {
        context.ReferralEvents.AddRange(events);
        await SaveAsync(ct);
    }

    public void AddEvents(IEnumerable<ReferralEvent> events) => context.ReferralEvents.AddRange(events);

    public async Task<bool> IsTeamMemberAsync(Guid userId, Guid teamId, CancellationToken ct) =>
        await context.TeamMemberships.AnyAsync(m => m.UserId == userId && m.TeamId == teamId, ct);

    public async Task<Referral?> GetForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await WithDetails(context.Referrals.AsNoTracking())
            .FirstOrDefaultAsync(r => r.PathwayId == pathwayId, ct);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesOrConflictAsync(ReferralWorkflow.ChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(ReferralClockPauseConfiguration.OpenPerReferralIndex))
        {
            throw new ConflictException(WaitingClock.PausedMeanwhileMessage);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetPauseDetailsAsync(
        IReadOnlyCollection<Guid> pauseIds, CancellationToken ct)
    {
        var ids = pauseIds.ToList();
        return await context.ReferralClockPauses
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Detail != null)
            .ToDictionaryAsync(p => p.Id, p => p.Detail!, ct);
    }

    public async Task<IReadOnlyList<ReferralEvent>> GetHistoryAsync(Guid referralId, CancellationToken ct) =>
        await context.ReferralEvents
            .AsNoTracking()
            .Where(e => e.ReferralId == referralId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id)
            .ToListAsync(ct);

    public async Task<int> CountLettersAsync(Guid referralId, CancellationToken ct) =>
        await context.Attachments.CountAsync(a => a.ReferralId == referralId && a.DeletedAtUtc == null, ct);

    public async Task<IReadOnlyList<Referral>> GetOpenForPatientAsync(Guid patientId, Guid teamId, CancellationToken ct)
    {
        var closed = PathwayWorkflow.ClosedStatuses.ToList();
        return await context.Referrals
            .AsNoTracking()
            .Where(r => r.PatientId == patientId
                        && r.TeamId == teamId
                        && (r.Status == ReferralStatus.AwaitingTriage
                            || r.Status == ReferralStatus.InformationRequested
                            || (r.Status == ReferralStatus.Accepted
                                && r.Pathway != null
                                && !closed.Contains(r.Pathway.Status))))
            .OrderBy(r => r.ReceivedOn).ThenBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ReferralRow>> SearchAsync(
        SearchReferralsQuery query, IReadOnlyCollection<Guid>? teamIds, DateOnly today, CancellationToken ct)
    {
        var filtered = context.Referrals.AsNoTracking().Filter(query, teamIds, today);
        var totalCount = await filtered.CountAsync(ct);

        var page = await filtered
            .Sort(query.Sort, query.Ascending, today)
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Select(r => new
            {
                Referral = r,
                r.Patient,
                r.Team,
                HasLetter = context.Attachments.Any(a => a.ReferralId == r.Id && a.DeletedAtUtc == null)
            })
            .ToListAsync(ct);

        var rows = page
            .Select(x =>
            {
                x.Referral.Patient = x.Patient;
                x.Referral.Team = x.Team;
                return new ReferralRow(x.Referral, x.HasLetter);
            })
            .ToList();
        return new PagedResult<ReferralRow>(rows, totalCount);
    }

    public async Task<int> CountAwaitingTriageAsync(IReadOnlyCollection<Guid>? teamIds, CancellationToken ct)
    {
        var awaiting = context.Referrals.Where(r => r.Status == ReferralStatus.AwaitingTriage);
        if (teamIds is not null)
        {
            var ids = teamIds.ToList();
            awaiting = awaiting.Where(r => ids.Contains(r.TeamId));
        }

        return await awaiting.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Referral>> GetAwaitingTriageForPatientAsync(Guid patientId, CancellationToken ct) =>
        await context.Referrals
            .AsNoTracking()
            .Include(r => r.Team)
            // Open: awaiting triage, or waiting for more information.
            .Where(r => r.PatientId == patientId
                        && (r.Status == ReferralStatus.AwaitingTriage || r.Status == ReferralStatus.InformationRequested))
            .OrderBy(r => r.ReceivedOn).ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReferralTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct) =>
        await ReferralTimelineSearch
            .Page(context.ReferralEvents.AsNoTracking(), context.Referrals.AsNoTracking(), query)
            .ToListAsync(ct);

    private static IQueryable<Referral> WithDetails(IQueryable<Referral> referrals) =>
        referrals
            .Include(r => r.Patient)
            .Include(r => r.Team)
            .Include(r => r.Pauses);
}
