using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Practices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>The GP practice directory. Patients and referrals point at it with restricting foreign keys.</summary>
public sealed class PracticeRepository(MedAppDbContext context) : IPracticeRepository
{
    /// <summary>The unique index on the ODS code, named as EF names it.</summary>
    private const string OdsCodeIndex = "IX_Practices_OdsCode";

    public async Task<IReadOnlyList<Practice>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.Practices
            .AsNoTracking()
            .Where(p => includeArchived || !p.IsArchived)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

    public async Task<Practice?> GetAsync(Guid id, CancellationToken ct) =>
        await context.Practices.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Practice>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return [];

        return await context.Practices.AsNoTracking().Where(p => list.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task<bool> OdsCodeExistsAsync(string odsCode, Guid? exceptId, CancellationToken ct) =>
        await context.Practices.AnyAsync(p => p.OdsCode == odsCode && (exceptId == null || p.Id != exceptId), ct);

    public async Task<IReadOnlyDictionary<Guid, PracticeUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
            return new Dictionary<Guid, PracticeUsage>();

        var patients = await context.Patients
            .Where(p => p.PracticeId != null && list.Contains(p.PracticeId.Value))
            .GroupBy(p => p.PracticeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var referrals = await context.Referrals
            .Where(r => r.PracticeId != null && list.Contains(r.PracticeId.Value))
            .GroupBy(r => r.PracticeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return patients.Keys.Concat(referrals.Keys)
            .Distinct()
            .ToDictionary(
                id => id,
                id => new PracticeUsage(patients.GetValueOrDefault(id), referrals.GetValueOrDefault(id)));
    }

    public async Task AddAsync(Practice practice, CancellationToken ct)
    {
        context.Practices.Add(practice);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another practice was given this code meanwhile.
        catch (DbUpdateException e) when (e.IsViolationOf(OdsCodeIndex))
        {
            context.Entry(practice).State = EntityState.Detached;
            throw new ConflictException(PracticeRules.OdsCodeTakenMessage(practice.OdsCode!));
        }
    }

    public async Task UpdateAsync(Practice practice, int originalVersion, CancellationToken ct)
    {
        var entry = context.Practices.Update(practice);
        // The version it was read at: the update matches only that row, so a change saved meanwhile is a 409.
        entry.Property(p => p.Version).OriginalValue = originalVersion;
        try
        {
            await context.SaveChangesOrConflictAsync(PracticeRules.ChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(OdsCodeIndex))
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(PracticeRules.OdsCodeTakenMessage(practice.OdsCode!));
        }
    }

    public async Task DeleteAsync(Practice practice, CancellationToken ct)
    {
        var entry = context.Practices.Remove(practice);
        try
        {
            await context.SaveChangesOrConflictAsync(PracticeRules.ChangedMeanwhileMessage, ct);
        }
        // Given to a patient or referral since it was checked: the foreign keys refuse.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 547 })
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(PracticeRules.JustUsedMessage);
        }
    }
}
