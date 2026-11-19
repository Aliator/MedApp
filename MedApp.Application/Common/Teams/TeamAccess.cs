using System.Linq.Expressions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// The one place the team walls' rules live (decision 202 of <c>docs/multi-clinic-plan.md</c>): LINQ expressions every
/// query on team data uses, so the same code runs in SQL and in unit tests, and the in-memory checks for writes.
/// <list type="bullet">
/// <item><b>A pathway</b> is seen in full by a scope that sees every team (admins, read only; the system), or with a
/// live grant (<see cref="PathwayAccess"/>) for one of the viewer's teams or the viewer themselves.</item>
/// <item><b>A record on a pathway</b> (a note, a file, a letter, a result, consent, an MDT item, a sample) follows its
/// pathway (<see cref="ByPathway{T}"/>); <b>one without</b> (a referral, an appointment without a stage, a
/// patient-level file) follows its own team (<see cref="ByTeam{T}"/>).</item>
/// <item><b>Only the owner team changes</b> what it owns (<see cref="CanChange(PatientPathway, TeamScope)"/>): admins
/// and the lab read.</item>
/// <item><b>Patients</b> are centre-wide: nothing here filters them.</item>
/// </list>
/// Not global query filters (Q1): clash checks, jobs, monitoring and uniqueness checks read every team's rows.
/// </summary>
public static class TeamAccess
{
    /// <summary>A grant that's live and is the viewer's: for one of their teams, or to them.</summary>
    public static Expression<Func<PathwayAccess, bool>> LiveGrantFor(TeamScope scope)
    {
        var teamIds = scope.TeamIds.ToList();
        var userId = scope.UserId;
        return a => a.EndedAtUtc == null
                    && ((a.TeamId != null && teamIds.Contains(a.TeamId.Value)) || a.UserId == userId);
    }

    /// <summary>
    /// The pathways the viewer sees in full. <paramref name="grants"/> is the grants table (the DbSet, or a list in a
    /// test), so it's one <c>EXISTS</c> in SQL.
    /// </summary>
    public static Expression<Func<PatientPathway, bool>> Pathways(TeamScope scope, IQueryable<PathwayAccess> grants)
    {
        if (scope.SeesAllTeams)
            return _ => true;

        var teamIds = scope.TeamIds.ToList();
        var userId = scope.UserId;
        return p => grants.Any(a => a.PathwayId == p.Id
                                    && a.EndedAtUtc == null
                                    && ((a.TeamId != null && teamIds.Contains(a.TeamId.Value)) || a.UserId == userId));
    }

    /// <summary>
    /// The IDs of pathways the viewer sees in full, as a subquery for another query (<c>ids.Contains(x.PathwayId)</c>).
    /// Only for a scope that doesn't see every team: check <see cref="TeamScope.SeesAllTeams"/> first.
    /// </summary>
    public static IQueryable<Guid> VisiblePathwayIds(TeamScope scope, IQueryable<PathwayAccess> grants) =>
        grants.Where(LiveGrantFor(scope)).Select(a => a.PathwayId);

    /// <summary>
    /// Records that follow their pathway, or their own team when they have none (MC5's letters, results, consent, MDT
    /// items, samples; MC4's files): seen when the record's team is one of the viewer's, or its pathway is seen in full.
    /// </summary>
    public static Expression<Func<T, bool>> ByPathway<T>(
        TeamScope scope,
        IQueryable<PathwayAccess> grants,
        Expression<Func<T, Guid?>> pathwayOf,
        Expression<Func<T, Guid>> teamOf)
    {
        if (scope.SeesAllTeams)
            return _ => true;

        var teamIds = scope.TeamIds.ToList();
        var userId = scope.UserId;
        Expression<Func<Guid?, Guid, bool>> rule = (pathwayId, teamId) =>
            teamIds.Contains(teamId)
            || (pathwayId != null && grants.Any(a => a.PathwayId == pathwayId
                                                     && a.EndedAtUtc == null
                                                     && ((a.TeamId != null && teamIds.Contains(a.TeamId.Value)) || a.UserId == userId)));
        return Compose(rule, pathwayOf, teamOf);
    }

    /// <summary>Records that belong to a team and no pathway (a referral, an appointment without a stage): the viewer's teams'.</summary>
    public static Expression<Func<T, bool>> ByTeam<T>(TeamScope scope, Expression<Func<T, Guid>> teamOf)
    {
        if (scope.SeesAllTeams)
            return _ => true;

        var teamIds = scope.TeamIds.ToList();
        Expression<Func<Guid, bool>> rule = teamId => teamIds.Contains(teamId);
        var parameter = teamOf.Parameters[0];
        return Expression.Lambda<Func<T, bool>>(new Replace(rule.Parameters[0], teamOf.Body).Visit(rule.Body)!, parameter);
    }

    /// <summary>Whether these grants (any of a pathway's) let the viewer see it in full.</summary>
    public static bool CanSee(IEnumerable<PathwayAccess> grants, TeamScope scope) =>
        scope.SeesAllTeams || grants.Any(LiveGrantFor(scope).Compile());

    /// <summary>
    /// The strongest reason the viewer sees a pathway (owner, stage owner, consult, MDT, then the system or an admin),
    /// or null when it's behind their wall. <paramref name="grants"/> may be any of the pathway's: only the viewer's
    /// live ones count.
    /// </summary>
    public static TeamAccessKind? Strongest(IEnumerable<PathwayAccess> grants, TeamScope scope)
    {
        var live = LiveGrantFor(scope).Compile();
        var reasons = grants.Where(live).Select(a => a.Reason).ToHashSet();
        if (reasons.Contains(PathwayAccessReason.Owner)) return TeamAccessKind.Owner;
        if (reasons.Contains(PathwayAccessReason.StageOwner)) return TeamAccessKind.StageOwner;
        if (reasons.Contains(PathwayAccessReason.Consult)) return TeamAccessKind.Consult;
        if (reasons.Contains(PathwayAccessReason.Mdt)) return TeamAccessKind.Mdt;
        if (scope.IsSystem) return TeamAccessKind.System;
        if (scope.SeesAllTeams) return TeamAccessKind.Admin;
        return null;
    }

    /// <summary>
    /// Whether the viewer may change a pathway: their team owns it (or it's the system). Admins and other teams with a
    /// grant (the lab, a consult, an MDT) only read it.
    /// </summary>
    public static bool CanChange(PatientPathway pathway, TeamScope scope) => CanChangeTeam(pathway.TeamId, scope);

    /// <summary>Whether the viewer's team owns a record of this team (or it's the system), so may change it.</summary>
    public static bool CanChangeTeam(Guid teamId, TeamScope scope) => scope.IsSystem || scope.IsMember(teamId);

    /// <summary>Whether the viewer sees a record of this team that has no pathway.</summary>
    public static bool CanSeeTeam(Guid teamId, TeamScope scope) => scope.SeesAllTeams || scope.IsMember(teamId);

    /// <summary><c>rule(pathwayOf(x), teamOf(x))</c> as one expression over <typeparamref name="T"/>, so EF translates it.</summary>
    private static Expression<Func<T, bool>> Compose<T>(
        Expression<Func<Guid?, Guid, bool>> rule,
        Expression<Func<T, Guid?>> pathwayOf,
        Expression<Func<T, Guid>> teamOf)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var pathway = new Replace(pathwayOf.Parameters[0], parameter).Visit(pathwayOf.Body)!;
        var team = new Replace(teamOf.Parameters[0], parameter).Visit(teamOf.Body)!;
        var body = new Replace(rule.Parameters[0], pathway).Visit(rule.Body)!;
        body = new Replace(rule.Parameters[1], team).Visit(body)!;
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class Replace(Expression from, Expression to) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node == from ? to : base.Visit(node);
    }
}
