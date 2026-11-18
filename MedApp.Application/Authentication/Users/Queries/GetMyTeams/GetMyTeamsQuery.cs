using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetMyTeams;

/// <summary>The teams the signed-in user belongs to, sorted by name.</summary>
public sealed record GetMyTeamsQuery(Guid UserId) : IRequest<IReadOnlyList<TeamMembershipResponse>>;
