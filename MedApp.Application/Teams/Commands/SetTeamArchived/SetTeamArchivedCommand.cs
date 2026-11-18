using MedApp.Contracts.Teams.Responses;
using MediatR;

namespace MedApp.Application.Teams.Commands.SetTeamArchived;

/// <summary>
/// Archives a team (it can't be chosen for templates or given to staff any more; what has it keeps it) or
/// restores it, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed since).
/// </summary>
public sealed record SetTeamArchivedCommand(Guid TeamId, bool IsArchived, int ExpectedVersion)
    : IRequest<TeamResponse>;
