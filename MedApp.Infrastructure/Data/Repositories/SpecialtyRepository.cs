using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Specialties, and staff memberships of them (deleted with the account, by the foreign key's cascade).</summary>
public sealed class SpecialtyRepository(MedAppDbContext context) : ISpecialtyRepository
{
    private const string NameIndex = "IX_Specialties_Name";

    public async Task<IReadOnlyList<Specialty>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.Specialties
            .AsNoTracking()
            .Where(s => includeArchived || !s.IsArchived)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task<Specialty?> GetAsync(Guid id, CancellationToken ct) =>
        await context.Specialties.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Specialty>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return [];

        return await context.Specialties.AsNoTracking().Where(s => list.Contains(s.Id)).ToListAsync(ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return await context.Specialties
            .AnyAsync(s => s.Name.ToLower() == lower && (exceptId == null || s.Id != exceptId), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, SpecialtyUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<Guid, SpecialtyUsage>();

        var templates = await context.PathwayTemplates
            .Where(t => list.Contains(t.SpecialtyId))
            .GroupBy(t => t.SpecialtyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var pathways = await context.PatientPathways
            .Where(p => list.Contains(p.SpecialtyId))
            .GroupBy(p => p.SpecialtyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var members = await context.StaffSpecialties
            .Where(m => list.Contains(m.SpecialtyId))
            .GroupBy(m => m.SpecialtyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var referrals = await context.Referrals
            .Where(r => list.Contains(r.SpecialtyId))
            .GroupBy(r => r.SpecialtyId)
            .Select(g => new
            {
                g.Key,
                Count = g.Count(),
                Awaiting = g.Count(r => r.Status == ReferralStatus.AwaitingTriage)
            })
            .ToDictionaryAsync(x => x.Key, ct);

        return templates.Keys.Concat(pathways.Keys).Concat(members.Keys).Concat(referrals.Keys)
            .Distinct()
            .ToDictionary(
                id => id,
                id => new SpecialtyUsage(
                    templates.GetValueOrDefault(id),
                    pathways.GetValueOrDefault(id),
                    members.GetValueOrDefault(id),
                    referrals.GetValueOrDefault(id)?.Count ?? 0,
                    referrals.GetValueOrDefault(id)?.Awaiting ?? 0));
    }

    public async Task AddAsync(Specialty specialty, CancellationToken ct)
    {
        context.Specialties.Add(specialty);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another admin added one with this name meanwhile.
        catch (DbUpdateException e) when (e.IsViolationOf(NameIndex))
        {
            context.Entry(specialty).State = EntityState.Detached;
            throw new ConflictException(SpecialtyRules.NameTakenMessage(specialty.Name));
        }
    }

    public async Task UpdateAsync(Specialty specialty, int originalVersion, CancellationToken ct)
    {
        var entry = context.Specialties.Update(specialty);
        // The version it was read at: the update matches only that row, so a change saved meanwhile is a 409.
        entry.Property(s => s.Version).OriginalValue = originalVersion;
        try
        {
            await context.SaveChangesOrConflictAsync(SpecialtyRules.ChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(NameIndex))
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(SpecialtyRules.NameTakenMessage(specialty.Name));
        }
    }

    public async Task DeleteAsync(Specialty specialty, CancellationToken ct)
    {
        var entry = context.Specialties.Remove(specialty);
        try
        {
            await context.SaveChangesOrConflictAsync(SpecialtyRules.ChangedMeanwhileMessage, ct);
        }
        // Given to a template, pathway or member of staff since it was checked: the foreign keys refuse.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 })
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(
                $"The '{specialty.Name}' specialty has just been given to something, so it can't be deleted. Archive it instead.");
        }
    }

    public async Task<IReadOnlyList<Specialty>> GetForUserAsync(Guid userId, CancellationToken ct) =>
        await context.StaffSpecialties
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.Specialty)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task SetForUserAsync(
        Guid userId, IReadOnlyCollection<Guid> specialtyIds, ActivityEntry activity, CancellationToken ct)
    {
        var current = await context.StaffSpecialties.Where(m => m.UserId == userId).ToListAsync(ct);
        var wanted = specialtyIds.ToHashSet();

        context.StaffSpecialties.RemoveRange(current.Where(m => !wanted.Contains(m.SpecialtyId)));
        var held = current.Select(m => m.SpecialtyId).ToHashSet();
        context.StaffSpecialties.AddRange(wanted
            .Where(id => !held.Contains(id))
            .Select(id => new StaffSpecialty { UserId = userId, SpecialtyId = id }));
        context.ActivityEntries.Add(activity);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another admin changed their specialties at the same moment (a membership added twice), or a specialty went.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 547 })
        {
            context.ChangeTracker.Clear();
            throw new ConflictException(
                "Someone changed this person's specialties at the same moment. Reload to see them, then try again.");
        }
    }
}
