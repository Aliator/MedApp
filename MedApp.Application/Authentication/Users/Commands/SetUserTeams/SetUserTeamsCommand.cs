using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserTeams;

/// <summary>
/// Sets the teams a member of staff belongs to, exactly, each with their role in it (an empty list removes them all).
/// Returns them as they are now.
/// </summary>
public sealed record SetUserTeamsCommand(string Username, IReadOnlyList<TeamChoice> Teams)
    : IRequest<IReadOnlyList<TeamMembershipResponse>>;

/// <summary>A team to belong to, and the role in it by name (<c>Member</c> when null, or <c>ClinicAdmin</c>).</summary>
public sealed record TeamChoice(Guid TeamId, string? Role = null);
