using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Queries.GetTeamById;

public sealed record GetTeamByIdQuery(Guid TeamId) : IRequest<TeamResponse>;
