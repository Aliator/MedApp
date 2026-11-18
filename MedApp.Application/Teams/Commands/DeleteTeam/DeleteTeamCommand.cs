using MediatR;

namespace MedApp.Application.Teams.Commands.DeleteTeam;

/// <summary>Deletes a team no template, pathway or member of staff has; one that's in use is archived instead (409).</summary>
public sealed record DeleteTeamCommand(Guid TeamId) : IRequest;
