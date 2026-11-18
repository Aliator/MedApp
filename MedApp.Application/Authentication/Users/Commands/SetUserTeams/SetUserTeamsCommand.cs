using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserTeams;

/// <summary>
/// Sets the teams a member of staff belongs to, exactly (an empty list removes them all). Returns them as they
/// are now.
/// </summary>
public sealed record SetUserTeamsCommand(string Username, IReadOnlyList<Guid> TeamIds)
    : IRequest<IReadOnlyList<TeamMembershipResponse>>;
