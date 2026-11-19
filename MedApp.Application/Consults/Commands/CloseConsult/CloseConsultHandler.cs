using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.CloseConsult;

/// <summary>
/// Closes a consult (MC25): only the asking team (a 409 for anyone else who sees it), only once it's replied (a 409
/// before: withdraw it instead), from the version the caller saw. Allowed while the record is restricted: it ends the
/// other team's reading (its grant, in the same save).
/// </summary>
public sealed class CloseConsultHandler(
    IConsultRepository repository,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<CloseConsultCommand, ConsultResponse>
{
    public async Task<ConsultResponse> Handle(CloseConsultCommand request, CancellationToken cancellationToken)
    {
        var consult = await repository.GetForChangeOrThrowAsync(
            request.ConsultId, boundary, request.Actor.Teams, cancellationToken);
        if (!request.Actor.Teams.IsMember(consult.AskingTeamId))
            throw new ConflictException(ConsultRules.NotAskingTeam(consult.AskingTeam?.Name));
        ConsultRules.EnsureVersion(consult, request.ExpectedVersion);
        if (ConsultRules.WhyCantClose(consult) is { } why)
            throw new ConflictException(why);

        var change = new ConsultChange(
            consult, consult.Pathway, consult.AskingTeam?.Name ?? string.Empty, consult.AskedTeam?.Name ?? string.Empty,
            request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.Closed();
        await repository.SaveAsync(change, cancellationToken);
        return ConsultMapper.ToResponse(consult, request.Actor);
    }
}
