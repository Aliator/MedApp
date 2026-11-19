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

    /// <summary>Someone's memberships, each with its team and their role in it, sorted by the team's name.</summary>
    Task<IReadOnlyList<TeamMembership>> GetMembershipsAsync(Guid userId, CancellationToken ct);

    /// <summary>Which of these people are the clinic admin of any of their teams (for their account type).</summary>
    Task<IReadOnlySet<Guid>> GetClinicAdminsAsync(IEnumerable<Guid> userIds, CancellationToken ct);

    /// <summary>The IDs of a team's members (MC7: who's told of a handover), each once.</summary>
    Task<IReadOnlyList<Guid>> GetMemberIdsAsync(Guid teamId, CancellationToken ct);

    /// <summary>The lab (the team of the lab kind that isn't archived), or null if there's none.</summary>
    Task<Team?> GetLabAsync(CancellationToken ct);

    /// <summary>
    /// Sets the teams a member of staff belongs to, exactly, each with their role in it, and saves
    /// <paramref name="activity"/> in the same save, so the change can't miss the activity log.
    /// </summary>
    Task SetForUserAsync(
        Guid userId, IReadOnlyDictionary<Guid, TeamMemberRole> teams, ActivityEntry activity, CancellationToken ct);
}
