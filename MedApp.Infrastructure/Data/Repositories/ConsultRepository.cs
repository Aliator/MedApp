using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data.Configurations.Consults;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Consults, each saved with its audit entry and the asked team's grant written or ended, in one save.</summary>
public sealed class ConsultRepository(MedAppDbContext context) : IConsultRepository
{
    public async Task<Consult?> GetAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Consults.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Consult?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Consults).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PatientPathway?> GetPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await context.PatientPathways.AsNoTracking()
            .Include(p => p.Patient)
            .FirstOrDefaultAsync(p => p.Id == pathwayId, ct);

    public async Task<IReadOnlyList<Consult>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct) =>
        await WithDetails(context.Consults.AsNoTracking())
            .Where(c => c.PathwayId == pathwayId)
            .OrderByDescending(c => c.RequestedAtUtc).ThenByDescending(c => c.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Consult>> GetIncomingAsync(Guid? teamId, TeamScope scope, CancellationToken ct)
    {
        var teamIds = scope.TeamIds.ToList();
        var seesAll = scope.SeesAllTeams;
        return await WithDetails(context.Consults.AsNoTracking())
            .Where(c => c.Status == ConsultStatus.Open || c.Status == ConsultStatus.Replied)
            .Where(c => seesAll || teamIds.Contains(c.AskedTeamId))
            .Where(c => teamId == null || c.AskedTeamId == teamId)
            .OrderBy(c => c.RequestedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public Task<bool> HasOpenAsync(Guid pathwayId, Guid askedTeamId, CancellationToken ct) =>
        context.Consults.AnyAsync(c => c.PathwayId == pathwayId
                                       && c.AskedTeamId == askedTeamId
                                       && (c.Status == ConsultStatus.Open || c.Status == ConsultStatus.Replied), ct);

    public async Task AddAsync(ConsultChange change, string askedTeamName, CancellationToken ct)
    {
        var consult = context.Consults.Add(change.Consult);
        context.PatientPathwayAuditEntries.AddRange(change.AuditEntries);
        var grant = change.NewGrant is { } newGrant ? context.PathwayAccess.Add(newGrant) : null;
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(ConsultConfiguration.OpenPerTeamIndex))
        {
            consult.State = EntityState.Detached;
            if (grant is not null)
                grant.State = EntityState.Detached;
            foreach (var entry in change.AuditEntries)
                context.Entry(entry).State = EntityState.Detached;
            throw new ConflictException(ConsultRules.AlreadyOpen(askedTeamName));
        }
    }

    public async Task SaveAsync(ConsultChange change, CancellationToken ct)
    {
        context.PatientPathwayAuditEntries.AddRange(change.AuditEntries);
        if (change.EndsGrant)
            await EndGrantsAsync(change.Consult, change.NowUtc, ct);
        await context.SaveChangesOrConflictAsync(ConsultRules.ChangedMeanwhileMessage, ct);
    }

    /// <summary>
    /// Ends the asked team's live consult grant on the pathway: at most one, as only one consult per team is open at a
    /// time. A grant the team holds for another reason (the lab's as a stage owner) stays.
    /// </summary>
    private async Task EndGrantsAsync(Consult consult, DateTime nowUtc, CancellationToken ct)
    {
        var grants = await context.PathwayAccess
            .Where(a => a.PathwayId == consult.PathwayId
                        && a.TeamId == consult.AskedTeamId
                        && a.Reason == PathwayAccessReason.Consult
                        && a.EndedAtUtc == null)
            .ToListAsync(ct);
        foreach (var grant in grants)
            grant.EndedAtUtc = nowUtc;
    }

    private static IQueryable<Consult> WithDetails(IQueryable<Consult> consults) =>
        consults
            .Include(c => c.Patient)
            .Include(c => c.Pathway).ThenInclude(p => p.Assignments)
            .Include(c => c.AskingTeam)
            .Include(c => c.AskedTeam)
            .AsSplitQuery();
}
