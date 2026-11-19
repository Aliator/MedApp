using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Consults.Responses;
using MedApp.Domain.Consults;
using MediatR;

namespace MedApp.Application.Consults.Commands.ReplyToConsult;

/// <summary>
/// Replies to a consult (MC25): only the asked team (a 409 for anyone else who sees it), once, from the version the
/// caller saw, while the record can change. Whoever sent it and the pathway's assignees are told (hazard H-75); the
/// reply is recorded on the pathway (its audit entry, without the text). The consult stays open, and the asked team
/// keeps reading the pathway, until the asking team closes it.
/// </summary>
public sealed class ReplyToConsultHandler(
    IConsultRepository repository,
    ITeamBoundary boundary,
    IConsultNotifications notifications,
    TimeProvider timeProvider)
    : IRequestHandler<ReplyToConsultCommand, ConsultResponse>
{
    public async Task<ConsultResponse> Handle(ReplyToConsultCommand request, CancellationToken cancellationToken)
    {
        var consult = await repository.GetForChangeOrThrowAsync(
            request.ConsultId, boundary, request.Actor.Teams, cancellationToken);
        if (!request.Actor.Teams.IsMember(consult.AskedTeamId))
            throw new ConflictException(ConsultRules.NotAskedTeam(consult.AskedTeam?.Name));
        ConsultRules.EnsureVersion(consult, request.ExpectedVersion);
        if (consult.Status != ConsultStatus.Open)
            throw new ConflictException(ConsultRules.AlreadyRepliedMessage);
        PatientRules.EnsureCanChange(consult.Patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var askedName = consult.AskedTeam?.Name ?? string.Empty;
        var change = new ConsultChange(
            consult, consult.Pathway, consult.AskingTeam?.Name ?? string.Empty, askedName, request.Actor, now);
        change.Replied(request.Reply.Trim());
        await repository.SaveAsync(change, cancellationToken);

        await notifications.RepliedAsync(change, askedName, cancellationToken);
        return ConsultMapper.ToResponse(consult, request.Actor);
    }
}
