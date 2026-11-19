using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.WithdrawConsult;

/// <summary>
/// Withdraws a consult (MC25): only the asking team (a 409 for anyone else who sees it), only before a reply (a 409
/// after: close it instead), from the version the caller saw. Allowed while the record is restricted: it ends the
/// other team's reading (its grant, in the same save).
/// </summary>
public sealed class WithdrawConsultHandler(
    IConsultRepository repository,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<WithdrawConsultCommand, ConsultResponse>
{
    public async Task<ConsultResponse> Handle(WithdrawConsultCommand request, CancellationToken cancellationToken)
    {
        var consult = await repository.GetForChangeOrThrowAsync(
            request.ConsultId, boundary, request.Actor.Teams, cancellationToken);
        if (!request.Actor.Teams.IsMember(consult.AskingTeamId))
            throw new ConflictException(ConsultRules.NotAskingTeam(consult.AskingTeam?.Name));
        ConsultRules.EnsureVersion(consult, request.ExpectedVersion);
        if (ConsultRules.WhyCantWithdraw(consult) is { } why)
            throw new ConflictException(why);

        var change = new ConsultChange(
            consult, consult.Pathway, consult.AskingTeam?.Name ?? string.Empty, consult.AskedTeam?.Name ?? string.Empty,
            request.Actor, timeProvider.GetUtcNow().UtcDateTime);
        change.Withdrawn();
        await repository.SaveAsync(change, cancellationToken);
        return ConsultMapper.ToResponse(consult, request.Actor);
    }
}
