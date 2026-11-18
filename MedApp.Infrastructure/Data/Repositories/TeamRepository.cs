using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Teams, and staff memberships of them (deleted with the account, by the foreign key's cascade).</summary>
public sealed class TeamRepository(MedAppDbContext context) : ITeamRepository
{
    private const string NameIndex = "IX_Teams_Name";

    public async Task<IReadOnlyList<Team>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.Teams
            .AsNoTracking()
            .Where(s => includeArchived || !s.IsArchived)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<Team?> GetAsync(Guid id, CancellationToken ct) =>
        await context.Teams.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Team>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return [];

        return await context.Teams.AsNoTracking().Where(s => list.Contains(s.Id)).ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.Teams
            .AnyAsync(s => s.Name.ToLower() == lower && (exceptId == null || s.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, TeamUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<Guid, TeamUsage>();

        var templates = await context.PathwayTemplates
            .Where(t => list.Contains(t.TeamId))
            .GroupBy(t => t.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var pathways = await context.PatientPathways
            .Where(p => list.Contains(p.TeamId))
            .GroupBy(p => p.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var members = await context.TeamMemberships
            .Where(m => list.Contains(m.TeamId))
            .GroupBy(m => m.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Members who can triage: they hold TriageReferrals through any of their roles.
        var triageHolders = RolePermissionQueries.HoldersOf(context, Permission.TriageReferrals);
        var triagers = await context.TeamMemberships
            .Where(m => list.Contains(m.TeamId) && triageHolders.Contains(m.UserId))
            .GroupBy(m => m.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var referrals = await context.Referrals
            .Where(r => list.Contains(r.TeamId))
            .GroupBy(r => r.TeamId)
            .Select(g => new
            {
                g.Key,
                Count = g.Count(),
                Awaiting = g.Count(r => r.Status == ReferralStatus.AwaitingTriage || r.Status == ReferralStatus.InformationRequested)
            })
            .ToDictionaryAsync(x => x.Key, ct);

        var letterTemplates = await context.LetterTemplates
            .Where(t => t.TeamId != null && list.Contains(t.TeamId.Value))
            .GroupBy(t => t.TeamId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // Clinic sessions and patterns name a team too (centre plan decision 123).
        var sessions = await context.ClinicSessions
            .Where(c => list.Contains(c.TeamId))
            .GroupBy(c => c.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var patterns = await context.ClinicPatterns
            .Where(c => list.Contains(c.TeamId))
            .GroupBy(c => c.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        // MDTs (meetings marked as a team's) and the items listed for them.
        var mdtMeetings = await context.Meetings
            .Where(m => m.TeamId != null && list.Contains(m.TeamId.Value))
            .GroupBy(m => m.TeamId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var mdtItems = await context.MdtItems
            .Where(i => list.Contains(i.TeamId))
            .GroupBy(i => i.TeamId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return templates.Keys.Concat(pathways.Keys).Concat(members.Keys).Concat(referrals.Keys)
            .Concat(sessions.Keys).Concat(patterns.Keys).Concat(letterTemplates.Keys)
            .Concat(mdtMeetings.Keys).Concat(mdtItems.Keys)
            .Distinct()
            .ToDictionary(
                id => id,
                id => new TeamUsage(
                    templates.GetValueOrDefault(id),
                    pathways.GetValueOrDefault(id),
                    members.GetValueOrDefault(id),
                    referrals.GetValueOrDefault(id)?.Count ?? 0,
                    referrals.GetValueOrDefault(id)?.Awaiting ?? 0,
                    triagers.GetValueOrDefault(id),
                    sessions.GetValueOrDefault(id) + patterns.GetValueOrDefault(id),
                    letterTemplates.GetValueOrDefault(id),
                    mdtMeetings.GetValueOrDefault(id) + mdtItems.GetValueOrDefault(id)));
    }

    public async Task AddAsync(Team team, CancellationToken ct)
    {
        context.Teams.Add(team);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another admin added one with this name meanwhile.
        catch (DbUpdateException e) when (e.IsViolationOf(NameIndex))
        {
            context.Entry(team).State = EntityState.Detached;
            throw new ConflictException(TeamRules.NameTakenMessage(team.Name));
        }
    }

    public async Task UpdateAsync(Team team, int originalVersion, CancellationToken ct)
    {
        var entry = context.Teams.Update(team);
        // The version it was read at: the update matches only that row, so a change saved meanwhile is a 409.
        entry.Property(s => s.Version).OriginalValue = originalVersion;
        try
        {
            await context.SaveChangesOrConflictAsync(TeamRules.ChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(NameIndex))
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(TeamRules.NameTakenMessage(team.Name));
        }
    }

    public async Task DeleteAsync(Team team, CancellationToken ct)
    {
        var entry = context.Teams.Remove(team);
        try
        {
            await context.SaveChangesOrConflictAsync(TeamRules.ChangedMeanwhileMessage, ct);
        }
        // Given to a template, pathway or member of staff since it was checked: the foreign keys refuse.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 })
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(
                $"The '{team.Name}' team has just been given to something, so it can't be deleted. Archive it instead.");
        }
    }

    public async Task<IReadOnlyList<Team>> GetForUserAsync(Guid userId, CancellationToken ct) =>
        await context.TeamMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.Team)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task SetForUserAsync(
        Guid userId, IReadOnlyCollection<Guid> teamIds, ActivityEntry activity, CancellationToken ct)
    {
        var current = await context.TeamMemberships.Where(m => m.UserId == userId).ToListAsync(ct);
        var wanted = teamIds.ToHashSet();

        context.TeamMemberships.RemoveRange(current.Where(m => !wanted.Contains(m.TeamId)));
        var held = current.Select(m => m.TeamId).ToHashSet();
        context.TeamMemberships.AddRange(wanted
            .Where(id => !held.Contains(id))
            .Select(id => new TeamMembership { UserId = userId, TeamId = id }));
        context.ActivityEntries.Add(activity);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another admin changed their teams at the same moment (a membership added twice), or a team went.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 547 })
        {
            context.ChangeTracker.Clear();
            throw new ConflictException(
                "Someone changed this person's teams at the same moment. Reload to see them, then try again.");
        }
    }
}
