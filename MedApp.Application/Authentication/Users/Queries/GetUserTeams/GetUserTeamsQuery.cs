using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetUserTeams;

/// <summary>The teams a member of staff belongs to, sorted by name; 404 for an unknown username.</summary>
public sealed record GetUserTeamsQuery(string Username) : IRequest<IReadOnlyList<TeamMembershipResponse>>;
