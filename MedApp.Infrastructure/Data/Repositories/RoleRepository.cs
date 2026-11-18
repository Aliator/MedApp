using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Authentication.Roles.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Identity's roles (<see cref="ApplicationRole"/>) and their permissions (<see cref="RolePermission"/>) for the Roles
/// and Permissions page. Changes are meant to run inside the handler's <c>IIdentityTransactions</c> transaction, which a
/// retry runs again from the start (forgetting what the failed attempt tracked).
/// </summary>
public sealed class RoleRepository(MedAppDbContext context) : IRoleRepository
{
    public async Task<IReadOnlyList<RoleData>> GetAllAsync(CancellationToken ct)
    {
        var roles = await context.Roles.AsNoTracking().Include(r => r.Permissions).ToListAsync(ct);
        return roles.Select(ToData).ToList();
    }

    public async Task<RoleData?> GetAsync(Guid id, CancellationToken ct)
    {
        var role = await context.Roles.AsNoTracking().Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
        return role is null ? null : ToData(role);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountHoldersAsync(CancellationToken ct) =>
        await context.UserRoles.AsNoTracking()
            .GroupBy(userRole => userRole.RoleId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyList<RoleHolder>> GetHoldersAsync(Guid roleId, CancellationToken ct)
    {
        var holders = await (
                from userRole in context.UserRoles.AsNoTracking()
                where userRole.RoleId == roleId
                join user in context.Users.AsNoTracking() on userRole.UserId equals user.Id
                select new { user.Id, user.UserName })
            .ToListAsync(ct);

        return holders
            .Select(h => new RoleHolder(h.Id, h.UserName ?? string.Empty))
            .OrderBy(h => h.Username, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.UserId)
            .ToList();
    }

    public async Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = Normalize(name);
        return await context.Roles.AnyAsync(r => r.NormalizedName == normalized && (exceptId == null || r.Id != exceptId), ct);
    }

    public async Task AddAsync(RoleData role, CancellationToken ct)
    {
        var entity = new ApplicationRole
        {
            Id = role.Id,
            Name = role.Name,
            NormalizedName = Normalize(role.Name),
            Description = role.Description,
            IsArchived = role.IsArchived,
            ConcurrencyStamp = role.Version,
            CreatedAt = role.CreatedAt,
            LastUpdated = role.LastUpdated,
            Permissions = role.StoredPermissions
                .Distinct(StringComparer.Ordinal)
                .Select(name => new RolePermission { RoleId = role.Id, Permission = name })
                .ToList(),
        };
        context.Roles.Add(entity);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Not saved: forget it, so nothing else saved in this request tries it again.
            foreach (var permission in entity.Permissions)
                context.Entry(permission).State = EntityState.Detached;
            context.Entry(entity).State = EntityState.Detached;
            throw new ConflictException(RoleRules.NameTakenMessage(role.Name));
        }
    }

    public async Task UpdateAsync(RoleData role, string expectedVersion, CancellationToken ct)
    {
        var normalized = Normalize(role.Name);
        int updated;
        try
        {
            // One statement, guarded by the version the change started from: inside the transaction it locks the row
            // until the end, so a second admin's save waits, then finds the version changed and updates nothing.
            updated = await context.Roles
                .Where(r => r.Id == role.Id && r.ConcurrencyStamp == expectedVersion && !r.IsBuiltIn)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Name, role.Name)
                    .SetProperty(r => r.NormalizedName, normalized)
                    .SetProperty(r => r.Description, role.Description)
                    .SetProperty(r => r.IsArchived, role.IsArchived)
                    .SetProperty(r => r.ConcurrencyStamp, role.Version)
                    .SetProperty(r => r.LastUpdated, role.LastUpdated), ct);
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            throw new ConflictException(RoleRules.NameTakenMessage(role.Name));
        }

        if (updated == 0)
            throw new ConflictException(RoleRules.ChangedMeanwhileMessage);

        var wanted = role.StoredPermissions.Distinct(StringComparer.Ordinal).ToList();
        await context.RolePermissions
            .Where(p => p.RoleId == role.Id && !wanted.Contains(p.Permission))
            .ExecuteDeleteAsync(ct);

        var kept = await context.RolePermissions.AsNoTracking()
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.Permission)
            .ToListAsync(ct);
        var added = wanted.Except(kept, StringComparer.Ordinal).ToList();
        if (added.Count == 0)
            return;

        context.RolePermissions.AddRange(added.Select(name => new RolePermission { RoleId = role.Id, Permission = name }));
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteIfUnheldAsync(Guid roleId, CancellationToken ct)
    {
        // Its permissions go with it (the foreign key's cascade). Nobody holding it is checked in the same statement.
        var deleted = await context.Roles
            .Where(r => r.Id == roleId && !r.IsBuiltIn && !context.UserRoles.Any(userRole => userRole.RoleId == roleId))
            .ExecuteDeleteAsync(ct);
        return deleted == 1;
    }

    /// <summary>Identity's normalised form of a role's name (its default normaliser, as <c>RoleManager</c> uses).</summary>
    private static string Normalize(string name) => name.Trim().Normalize().ToUpperInvariant();

    private static RoleData ToData(ApplicationRole role) =>
        new()
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Description = role.Description,
            IsArchived = role.IsArchived,
            IsBuiltIn = role.IsBuiltIn,
            Version = role.ConcurrencyStamp ?? string.Empty,
            CreatedAt = role.CreatedAt,
            LastUpdated = role.LastUpdated,
            StoredPermissions = role.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal).ToList(),
        };
}
