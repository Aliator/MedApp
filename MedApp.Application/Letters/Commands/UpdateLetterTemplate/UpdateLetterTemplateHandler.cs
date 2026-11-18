using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.UpdateLetterTemplate;

/// <summary>
/// Edits a letter template (archived or not), from the caller's version (409 if it changed since). A newly chosen
/// team must exist and not be archived (an archived one may stay); the name must be free in its team (409).
/// Nothing is saved if nothing changed.
/// </summary>
public sealed class UpdateLetterTemplateHandler(
    ILetterTemplateRepository repository,
    ITeamRepository teams,
    TimeProvider timeProvider) : IRequestHandler<UpdateLetterTemplateCommand, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(UpdateLetterTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        LetterTemplateRules.EnsureVersion(template, request.ExpectedVersion);

        string? teamName = null;
        if (request.TeamId is { } teamId)
        {
            var alreadyGiven = template.TeamId is { } current ? new HashSet<Guid> { current } : [];
            var chosen = await TeamRules.CheckChosenAsync(teams, [teamId], alreadyGiven, cancellationToken);
            teamName = chosen[teamId].Name;
        }

        var name = request.Name.Trim();
        var kind = PathwayEnums.Parse<LetterKind>(request.Kind);
        var letterhead = LetterTemplateRules.Tidy(request.Letterhead);
        var body = LetterTemplateRules.Tidy(request.BodySkeleton);
        var closing = LetterTemplateRules.Tidy(request.Closing);

        if (template.Name == name && template.Kind == kind && template.TeamId == request.TeamId
            && template.Letterhead == letterhead && template.BodySkeleton == body && template.Closing == closing)
            return LetterTemplateMapper.ToResponse(
                template, teamName, !await repository.IsInUseAsync(template.Id, cancellationToken));

        var nameOrTeamChanged = !string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)
                                     || template.TeamId != request.TeamId;
        if (nameOrTeamChanged
            && await repository.NameExistsAsync(name, request.TeamId, template.Id, cancellationToken))
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(name, teamName));

        var originalVersion = template.Version;
        template.Name = name;
        template.Kind = kind;
        template.TeamId = request.TeamId;
        template.Letterhead = letterhead;
        template.BodySkeleton = body;
        template.Closing = closing;
        template.Version++;
        template.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(template, originalVersion, cancellationToken);

        return LetterTemplateMapper.ToResponse(
            template, teamName, !await repository.IsInUseAsync(template.Id, cancellationToken));
    }
}
