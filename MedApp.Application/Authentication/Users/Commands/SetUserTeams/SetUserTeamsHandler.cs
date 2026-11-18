using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Teams.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserTeams;

/// <summary>
/// Sets which teams someone belongs to. Every team must exist (404), and an archived one can only be kept,
/// not newly given (409). A change is recorded in the activity log (<see cref="ActivityAction.TeamMembershipChanged"/>,
/// with the teams before and after) in the same save; setting what they already have records nothing.
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

        var before = await repository.GetForUserAsync(user.UserId, cancellationToken);
        var beforeIds = before.Select(s => s.Id).ToHashSet();

        var chosen = await TeamRules.CheckChosenAsync(repository, request.TeamIds, beforeIds, cancellationToken);
        if (beforeIds.SetEquals(chosen.Keys))
            return TeamMapper.ToStaffResponses(before);

        var after = TeamMapper.ToStaffResponses(chosen.Values);
        var from = TeamMapper.ToStaffResponses(before);
        var entry = activityLog.Entry(
            ActivityAction.TeamMembershipChanged,
            targetUsername: user.Username,
            details: new
            {
                from = from.Select(s => s.Name).ToList(),
                to = after.Select(s => s.Name).ToList(),
                fromIds = from.Select(s => s.Id).ToList(),
                toIds = after.Select(s => s.Id).ToList()
            });

        await repository.SetForUserAsync(user.UserId, chosen.Keys.ToList(), entry, cancellationToken);

        return after;
    }
}
