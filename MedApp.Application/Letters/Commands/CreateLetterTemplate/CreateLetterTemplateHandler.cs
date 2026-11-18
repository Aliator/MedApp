using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.CreateLetterTemplate;

/// <summary>
/// Adds a letter template. Its team must exist and not be archived (404, 409); its name must be free in that
/// team (409).
/// </summary>
public sealed class CreateLetterTemplateHandler(
    ILetterTemplateRepository repository,
    ITeamRepository teams,
    TimeProvider timeProvider) : IRequestHandler<CreateLetterTemplateCommand, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(CreateLetterTemplateCommand request, CancellationToken cancellationToken)
    {
        string? teamName = null;
        if (request.TeamId is { } teamId)
        {
            var chosen = await TeamRules.CheckChosenAsync(
                teams, [teamId], new HashSet<Guid>(), cancellationToken);
            teamName = chosen[teamId].Name;
        }

        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, request.TeamId, null, cancellationToken))
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(name, teamName));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var template = new LetterTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = PathwayEnums.Parse<LetterKind>(request.Kind),
            TeamId = request.TeamId,
            Letterhead = LetterTemplateRules.Tidy(request.Letterhead),
            BodySkeleton = LetterTemplateRules.Tidy(request.BodySkeleton),
            Closing = LetterTemplateRules.Tidy(request.Closing),
            Version = 1,
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddAsync(template, cancellationToken);
        return LetterTemplateMapper.ToResponse(template, teamName, canBeDeleted: true);
    }
}
