using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserTeams;

/// <summary>
/// Sets which teams someone belongs to, and their role in each (a member, or the clinic's admin). Every team must exist
/// (404), and an archived one can only be kept, not newly given (409). An admin is in no team (409,
/// <see cref="TeamRules.AdminHasNoTeam"/>), and the lab has no clinic admin (409). A team given twice counts once (the
/// first). A change is recorded in the activity log (<see cref="ActivityAction.TeamMembershipChanged"/>, with the teams
/// before and after, a clinic admin's marked) in the same save; setting what they already have records nothing.
/// </summary>
public sealed class SetUserTeamsHandler(
    IIdentityReadService readService,
    ITeamRepository repository,
    IActivityLog activityLog)
    : IRequestHandler<SetUserTeamsCommand, IReadOnlyList<TeamMembershipResponse>>
{
    public async Task<IReadOnlyList<TeamMembershipResponse>> Handle(
        SetUserTeamsCommand request,
        CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");

        // Each team once, with its role (the validator has checked the roles).
        var wanted = new Dictionary<Guid, TeamMemberRole>();
        foreach (var choice in request.Teams)
            wanted.TryAdd(choice.TeamId, TeamRules.ParseRole(choice.Role) ?? TeamMemberRole.Member);

        var before = await repository.GetMembershipsAsync(user.UserId, cancellationToken);
        var beforeIds = before.Select(m => m.TeamId).ToHashSet();

        // An admin is in no team (decision 209): someone who is both has two accounts.
        if (wanted.Count > 0 && user.Roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
            throw new ConflictException(TeamRules.AdminHasNoTeam(user.Username));

        var chosen = await TeamRules.CheckChosenAsync(repository, wanted.Keys.ToList(), beforeIds, cancellationToken);
        if (wanted.Any(w => w.Value == TeamMemberRole.ClinicAdmin && chosen[w.Key].Kind == TeamKind.Lab))
            throw new ConflictException(TeamRules.LabHasNoClinicAdmin);

        var from = TeamMapper.ToStaffResponses(before);
        var after = TeamMapper.ToStaffResponses(wanted.Select(w => new TeamMembership
        {
            UserId = user.UserId,
            TeamId = w.Key,
            Team = chosen[w.Key],
            Role = w.Value
        }));

        var same = beforeIds.SetEquals(wanted.Keys) && before.All(m => wanted[m.TeamId] == m.Role);
        if (same)
            return from;

        var entry = activityLog.Entry(
            ActivityAction.TeamMembershipChanged,
            targetUsername: user.Username,
            details: new
            {
                from = from.Select(TeamMapper.ActivityName).ToList(),
                to = after.Select(TeamMapper.ActivityName).ToList(),
                fromIds = from.Select(s => s.Id).ToList(),
                toIds = after.Select(s => s.Id).ToList(),
                fromRoles = from.Select(s => s.Role).ToList(),
                toRoles = after.Select(s => s.Role).ToList()
            });

        await repository.SetForUserAsync(user.UserId, wanted, entry, cancellationToken);

        return after;
    }
}
