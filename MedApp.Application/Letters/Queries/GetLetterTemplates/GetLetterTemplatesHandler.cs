using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterTemplates;

/// <summary>A setup list: a few dozen at most, sorted here once their teams' names are known.</summary>
public sealed class GetLetterTemplatesHandler(ILetterTemplateRepository repository, ITeamRepository teams)
    : IRequestHandler<GetLetterTemplatesQuery, IReadOnlyList<LetterTemplateResponse>>
{
    public async Task<IReadOnlyList<LetterTemplateResponse>> Handle(
        GetLetterTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var teamIds = templates.Where(t => t.TeamId is not null).Select(t => t.TeamId!.Value).Distinct();
        var names = (await teams.GetByIdsAsync(teamIds, cancellationToken)).ToDictionary(s => s.Id, s => s.Name);
        var inUse = await repository.GetInUseAsync(templates.Select(t => t.Id).ToList(), cancellationToken);

        return templates
            .Select(t => LetterTemplateMapper.ToResponse(
                t, t.TeamId is { } id ? names.GetValueOrDefault(id) : null, !inUse.Contains(t.Id)))
            .OrderBy(t => t.TeamId is null ? 0 : 1)
            .ThenBy(t => t.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id)
            .ToList();
    }
}
