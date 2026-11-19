using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Consults.Responses;
using MedApp.Domain.Consults;
using MediatR;

namespace MedApp.Application.Consults.Commands.RequestConsult;

/// <summary>
/// Sends a consult (MC25): only the pathway's own team (another team's is a 404 behind the wall, recorded, or a 409 when
/// seen), while the pathway is open and the record can change, to another team in use (a 400 otherwise), and once at a
/// time per team asked (a 409). The asked team reads the pathway from now until it closes (the grant, saved with it), and
/// its members who work pathways are told. Nothing is suggested: the team and the words are the clinician's.
/// </summary>
public sealed class RequestConsultHandler(
    IConsultRepository repository,
    ITeamRepository teams,
    ITeamBoundary boundary,
    IConsultNotifications notifications,
    TimeProvider timeProvider)
    : IRequestHandler<RequestConsultCommand, ConsultResponse>
{
    public async Task<ConsultResponse> Handle(RequestConsultCommand request, CancellationToken cancellationToken)
    {
        var notFound = PathwayRepositoryExtensions.NotFoundMessage(request.PathwayId);
        var pathway = await repository.GetPathwayAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException(notFound);
        await boundary.EnsurePathwayVisibleAsync(pathway, request.Actor.Teams, notFound, cancellationToken);

        var askingTeam = await teams.GetAsync(pathway.TeamId, cancellationToken);
        if (!request.Actor.Teams.IsMember(pathway.TeamId))
            throw new ConflictException(ConsultRules.NotPathwaysTeam(askingTeam?.Name));
        if (ConsultRules.ClosedPathway.Contains(pathway.Status))
            throw new ConflictException(ConsultRules.ClosedPathwayMessage);
        PatientRules.EnsureCanChange(pathway.Patient);

        var askedTeam = await teams.GetAsync(request.AskedTeamId, cancellationToken);
        if (askedTeam is null || askedTeam.IsArchived)
            throw Invalid(ConsultRules.TeamNotInUseMessage);
        if (askedTeam.Id == pathway.TeamId)
            throw Invalid(ConsultRules.OwnTeamMessage);
        if (await repository.HasOpenAsync(pathway.Id, askedTeam.Id, cancellationToken))
            throw new ConflictException(ConsultRules.AlreadyOpen(askedTeam.Name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var consult = new Consult
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            AskingTeamId = pathway.TeamId,
            AskedTeamId = askedTeam.Id,
            Question = request.Question.Trim(),
            RequestedByUserId = request.Actor.UserId,
            RequestedByUsername = request.Actor.Username,
            RequestedAtUtc = now,
        };
        var askingName = askingTeam?.Name ?? string.Empty;
        var change = new ConsultChange(consult, pathway, askingName, askedTeam.Name, request.Actor, now);
        change.Requested();
        await repository.AddAsync(change, askedTeam.Name, cancellationToken);

        await notifications.RequestedAsync(change, askingName, cancellationToken);

        var saved = await repository.GetAsync(consult.Id, cancellationToken)
                    ?? throw new NotFoundException(ConsultRules.NotFoundMessage(consult.Id));
        return ConsultMapper.ToResponse(saved, request.Actor);
    }

    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure(nameof(RequestConsultCommand.AskedTeamId), message)]);
}
