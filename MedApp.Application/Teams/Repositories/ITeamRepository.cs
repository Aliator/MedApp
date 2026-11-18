using MedApp.Application.Teams.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;

namespace MedApp.Application.Teams.Repositories;

/// <summary>Teams, and which members of staff belong to which.</summary>
public interface ITeamRepository
{
    /// <summary>Every team, sorted by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Team>> GetAllAsync(bool includeArchived, CancellationToken ct);

    Task<Team?> GetAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Team>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another team (not <paramref name="exceptId"/>) already has this name, ignoring case.</summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How many templates, pathways and members of staff have each of these teams (only those any have).</summary>
    Task<IReadOnlyDictionary<Guid, TeamUsage>> GetUsageAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Adds a team. Another with the same name, added meanwhile, is a 409.</summary>
    Task AddAsync(Team team, CancellationToken ct);

    /// <summary>
    /// Saves a change to a team read as <paramref name="originalVersion"/>: if it has changed since (another
    /// admin saved first), nothing is saved and it's a 409. Another with the same name, saved meanwhile, is a 409 too.
    /// </summary>
    Task UpdateAsync(Team team, int originalVersion, CancellationToken ct);

    /// <summary>Deletes a team. One that came into use meanwhile (the database refuses) is a 409.</summary>
    Task DeleteAsync(Team team, CancellationToken ct);

    /// <summary>The teams a member of staff belongs to, sorted by name.</summary>
    Task<IReadOnlyList<Team>> GetForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Sets the teams a member of staff belongs to, exactly, and saves <paramref name="activity"/> in the same
    /// save, so the change can't miss the activity log.
    /// </summary>
    Task SetForUserAsync(Guid userId, IReadOnlyCollection<Guid> teamIds, ActivityEntry activity, CancellationToken ct);
}
