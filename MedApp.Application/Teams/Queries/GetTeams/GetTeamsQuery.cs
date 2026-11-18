using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Queries.GetTeams;

/// <summary>Every team, sorted by name, with what has each; archived ones only when asked for.</summary>
public sealed record GetTeamsQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<TeamResponse>>;
