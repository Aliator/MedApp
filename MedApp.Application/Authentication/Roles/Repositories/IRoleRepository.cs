using MedApp.Application.Authentication.Roles.Common;

namespace MedApp.Application.Authentication.Roles.Repositories;

/// <summary>
/// Roles and their permissions, for the Roles and Permissions page. Membership (who holds which role) is changed through
/// <c>IIdentityRoleService</c>; this only reads it. The changes are meant to run inside
/// <c>IIdentityTransactions.RunAsync</c> with their activity log entry, so both are kept or neither.
/// </summary>
public interface IRoleRepository
{
    /// <summary>Every role, archived ones too, with its stored permissions.</summary>
    Task<IReadOnlyList<RoleData>> GetAllAsync(CancellationToken ct);

    Task<RoleData?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>How many accounts hold each role (roles nobody holds are left out).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountHoldersAsync(CancellationToken ct);

    /// <summary>Who holds the role, sorted by username ignoring case.</summary>
    Task<IReadOnlyList<RoleHolder>> GetHoldersAsync(Guid roleId, CancellationToken ct);

    /// <summary>Whether another role (not <paramref name="exceptId"/>) already has this name, ignoring case.</summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Adds a role with its permissions. Another with the same name, added meanwhile, is a 409.</summary>
    Task AddAsync(RoleData role, CancellationToken ct);

    /// <summary>
    /// Saves a change to an editable role (its name, description, archived flag, version, last updated and exactly its
    /// permissions), only if it's still at <paramref name="expectedVersion"/>: otherwise another admin saved first, and
    /// it's a 409 with nothing saved. Inside a transaction the role's row stays locked until it ends, so a second admin
    /// saving at the same moment waits, then gets the 409. Another role with the name, saved meanwhile, is a 409 too.
    /// </summary>
    Task UpdateAsync(RoleData role, string expectedVersion, CancellationToken ct);

    /// <summary>
    /// Deletes an editable role (with its permissions) if nobody holds it, in one statement, so it can't take the role
    /// from someone given it at that moment. False if it wasn't deleted (someone holds it, or it's gone).
    /// </summary>
    Task<bool> DeleteIfUnheldAsync(Guid roleId, CancellationToken ct);
}
