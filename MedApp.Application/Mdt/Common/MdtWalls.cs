using MedApp.Application.Common.Teams;
using MedApp.Domain.Mdt;
using MedApp.Domain.Teams;

namespace MedApp.Application.Mdt.Common;

/// <summary>
/// The team walls on MDT items (MC5 of <c>docs/multi-clinic-plan.md</c>; the light demo's <c>mdt.ts</c>). An item belongs
/// to its MDT's team (<see cref="MdtItem.TeamId"/>) and follows its pathway (<see cref="TeamAccess.ByPathway{T}"/>):
/// the viewer sees it when it's their team's MDT, or they see its pathway. Lists and counts leave out the rest; one item
/// behind the wall is a 404 with the words of a missing one, recorded. Only the item's own team moves or withdraws it
/// (<see cref="EnsureCanChangeAsync"/>); listing a pathway needs the pathway's own team.
/// </summary>
public static class MdtWalls
{
    /// <summary>What a refusal across a team wall records as <c>targetKind</c> for an MDT item.</summary>
    public const string ItemTarget = "MdtItem";

    /// <summary>What a 409 calls an item another team owns (<see cref="TeamWalls.RecordNotYours"/>).</summary>
    public const string ItemWhat = "This MDT item";

    /// <summary>The 404's words for an item that doesn't exist, and for one behind the viewer's wall (the same).</summary>
    public static string NotFoundMessage(Guid id) => $"MDT item '{id}' was not found.";

    /// <summary>
    /// Only the items the viewer may see. <paramref name="grants"/> is the grants table (the DbSet, or a list in a test),
    /// needed unless the viewer sees every team.
    /// </summary>
    public static IQueryable<MdtItem> Visible(
        this IQueryable<MdtItem> items, TeamScope scope, IQueryable<PathwayAccess>? grants = null) =>
        scope.SeesAllTeams
            ? items
            : items.Where(TeamAccess.ByPathway<MdtItem>(
                scope, grants ?? throw new ArgumentNullException(nameof(grants)), i => (Guid?)i.PathwayId, i => i.TeamId));

    /// <summary>The item seen by the viewer (why, for the activity log), or a 404 (recorded) when it's behind their wall.</summary>
    public static Task<TeamAccessKind> EnsureVisibleAsync(
        this ITeamBoundary boundary, MdtItem item, TeamScope scope, CancellationToken ct) =>
        boundary.EnsureRecordVisibleAsync(ItemTarget, item.Id, item.TeamId, item.PathwayId, scope, NotFoundMessage(item.Id), ct);

    /// <summary>
    /// For a change only the item's own team makes (defer, withdraw): seen (else a 404, recorded) and its MDT's team the
    /// viewer's (else a 409: admins, and attendees from other teams, only read).
    /// </summary>
    public static async Task EnsureCanChangeAsync(this ITeamBoundary boundary, MdtItem item, TeamScope scope, CancellationToken ct)
    {
        await boundary.EnsureVisibleAsync(item, scope, ct);
        await boundary.EnsureCanChangeTeamAsync(item.TeamId, scope, ItemWhat, ct);
    }
}
