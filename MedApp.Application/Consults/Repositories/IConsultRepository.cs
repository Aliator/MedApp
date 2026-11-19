using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consults.Repositories;

/// <summary>
/// Consults (MC25), each read with its patient, pathway (and its assignees) and both teams. Every change saves its audit
/// entry, and the asked team's grant written or ended, in the same save (<see cref="ConsultChange"/>); a save made from a
/// copy read before someone else's is a 409 (<see cref="ConsultRules.ChangedMeanwhileMessage"/>).
/// </summary>
public interface IConsultRepository
{
    /// <summary>A consult with everything its response shows, read-only.</summary>
    Task<Consult?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>A consult with everything its response shows, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Consult?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>A pathway with its patient (no stages), read-only: what a consult about it needs to know.</summary>
    Task<PatientPathway?> GetPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>A pathway's consults, in any status, newest sent first (then by ID).</summary>
    Task<IReadOnlyList<Consult>> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>
    /// Open consults (open or replied) asked of the viewer's teams (every team's for an admin), in one team or all,
    /// oldest sent first (then by ID).
    /// </summary>
    Task<IReadOnlyList<Consult>> GetIncomingAsync(Guid? teamId, TeamScope scope, CancellationToken ct);

    /// <summary>Whether the pathway has an open consult (open or replied) with this team.</summary>
    Task<bool> HasOpenAsync(Guid pathwayId, Guid askedTeamId, CancellationToken ct);

    /// <summary>
    /// Adds a new consult (<see cref="ConsultChange.Requested"/>) with its audit entry and the asked team's grant. One
    /// already open with the same team since it was checked is a 409 (<see cref="ConsultRules.AlreadyOpen"/>).
    /// </summary>
    Task AddAsync(ConsultChange change, string askedTeamName, CancellationToken ct);

    /// <summary>
    /// Saves a change to a consult loaded with <see cref="GetForUpdateAsync"/>: its audit entry, and the end of the asked
    /// team's grant when it closes it.
    /// </summary>
    Task SaveAsync(ConsultChange change, CancellationToken ct);
}
