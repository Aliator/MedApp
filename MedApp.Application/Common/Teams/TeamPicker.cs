using FluentValidation;
using FluentValidation.Results;

namespace MedApp.Application.Common.Teams;

/// <summary>
/// The clinic picker on the grids' searches (MC6 of <c>docs/multi-clinic-plan.md</c>; the light demo's
/// <c>pickedTeam</c>): a <c>TeamId</c> narrows a list to one team, or none means all the viewer's. An admin (who sees
/// every team) may pick any team; anyone else only one of their own, else a 400 on <c>TeamId</c>. The walls still
/// apply: the pick only narrows what the viewer may see.
/// </summary>
public static class TeamPicker
{
    /// <summary>The 400's words when someone picks a team that isn't theirs.</summary>
    public const string NotYourTeam = "Choose one of your own teams, or all of them.";

    /// <summary>What an admin's list view records as <c>teamFilter</c> when no team is picked.</summary>
    public const string AllTeams = "all";

    /// <summary>Whether the viewer may pick this team (or none).</summary>
    public static bool CanPick(Guid? teamId, TeamScope scope) =>
        teamId is not { } id || scope.SeesAllTeams || scope.IsMember(id);

    /// <summary>Refuses (a 400 on <c>TeamId</c>) a team the viewer can't pick.</summary>
    public static void EnsureCanPick(Guid? teamId, TeamScope scope)
    {
        if (!CanPick(teamId, scope))
            throw new ValidationException([new ValidationFailure("TeamId", NotYourTeam)]);
    }

    /// <summary>
    /// What a list view's activity entries add (decision 207): an admin's records <c>access</c> <c>Admin</c> and the
    /// picker's choice as <c>teamFilter</c> (the team's ID, or <see cref="AllTeams"/>); anyone else's nothing (a list
    /// holds only what their teams may see, by more than one reason). Never a patient's details.
    /// </summary>
    public static object? ListDetails(Guid? teamId, TeamScope scope) =>
        scope.SeesAllTeams && !scope.IsSystem
            ? new { access = nameof(TeamAccessKind.Admin), teamFilter = teamId?.ToString() ?? AllTeams }
            : null;
}
