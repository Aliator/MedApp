using MedApp.Application.Common.Teams;
using MedApp.Domain.Lab;
using MedApp.Domain.Specimens;
using MedApp.Domain.Teams;

namespace MedApp.Application.Lab.Common;

/// <summary>
/// The team walls for samples and lab cases (decision 200, MC14 and MC15 of <c>docs/multi-clinic-plan.md</c>; the light
/// demo's <c>canSeeSample</c> and <c>canSeeCase</c>): the lab and admins see every one; a clinic sees its own clinic's
/// (its team's, or on a pathway it sees), never a hospital's. LINQ that runs in SQL and in memory.
/// </summary>
public static class LabWalls
{
    public const string SpecimenTarget = "Specimen";
    public const string LabCaseTarget = "LabCase";

    /// <summary>Whether the scope is the lab team's (a member of it).</summary>
    public static bool IsLabMember(TeamScope scope, Guid? labTeamId) => labTeamId is { } lab && scope.IsMember(lab);

    /// <summary>Whether the scope sees every sample and case: the lab, admins, and the system.</summary>
    public static bool SeesAll(TeamScope scope, Guid? labTeamId) => scope.SeesAllTeams || IsLabMember(scope, labTeamId);

    /// <summary>Only the samples the viewer may see.</summary>
    public static IQueryable<Specimen> VisibleSpecimens(
        this IQueryable<Specimen> specimens, TeamScope scope, IQueryable<PathwayAccess> grants, Guid? labTeamId) =>
        SeesAll(scope, labTeamId)
            ? specimens
            : specimens
                .Where(s => s.Source == SpecimenSource.Clinic)
                .Where(TeamAccess.ByPathway<Specimen>(scope, grants, s => s.PathwayId, s => s.TeamId));

    /// <summary>Only the lab cases the viewer may see (a clinic case by its clinic, or through its pathway).</summary>
    public static IQueryable<LabCase> VisibleCases(
        this IQueryable<LabCase> cases, TeamScope scope, IQueryable<PathwayAccess> grants, Guid? labTeamId) =>
        SeesAll(scope, labTeamId)
            ? cases
            : cases
                .Where(c => c.Source == SpecimenSource.Clinic && c.RequestingTeamId != null)
                .Where(TeamAccess.ByPathway<LabCase>(scope, grants, c => c.PathwayId, c => c.RequestingTeamId!.Value));

    /// <summary>
    /// At one sample: seen by the lab and admins, else a clinic's by its own team or through its pathway (the recorded
    /// 404 otherwise, as a missing one). Returns why it's seen, for the activity log's <c>access</c>.
    /// </summary>
    public static async Task<TeamAccessKind> EnsureSpecimenVisibleAsync(
        this ITeamBoundary boundary, Specimen specimen, TeamScope scope, Guid? labTeamId, string notFoundMessage, CancellationToken ct)
    {
        if (scope.SeesAllTeams)
            return scope.IsSystem ? TeamAccessKind.System : TeamAccessKind.Admin;
        if (IsLabMember(scope, labTeamId))
            return TeamAccessKind.Owner;
        if (specimen.Source != SpecimenSource.Clinic)
            throw await boundary.RefuseAsync(SpecimenTarget, specimen.Id, notFoundMessage, ct);
        return await boundary.EnsureRecordVisibleAsync(
            SpecimenTarget, specimen.Id, specimen.TeamId, specimen.PathwayId, scope, notFoundMessage, ct);
    }

    /// <summary>At one lab case: as a sample (the lab and admins every one, a clinic its own clinic's).</summary>
    public static async Task<TeamAccessKind> EnsureCaseVisibleAsync(
        this ITeamBoundary boundary, LabCase labCase, TeamScope scope, Guid? labTeamId, string notFoundMessage, CancellationToken ct)
    {
        if (scope.SeesAllTeams)
            return scope.IsSystem ? TeamAccessKind.System : TeamAccessKind.Admin;
        if (IsLabMember(scope, labTeamId))
            return TeamAccessKind.Owner;
        if (labCase.Source != SpecimenSource.Clinic || labCase.RequestingTeamId is not { } clinic)
            throw await boundary.RefuseAsync(LabCaseTarget, labCase.Id, notFoundMessage, ct);
        return await boundary.EnsureRecordVisibleAsync(
            LabCaseTarget, labCase.Id, clinic, labCase.PathwayId, scope, notFoundMessage, ct);
    }
}
