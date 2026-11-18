using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterTemplateById;

public sealed class GetLetterTemplateByIdHandler(ILetterTemplateRepository repository, ITeamRepository teams)
    : IRequestHandler<GetLetterTemplateByIdQuery, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(GetLetterTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        var team = template.TeamId is { } id ? await teams.GetAsync(id, cancellationToken) : null;
        return LetterTemplateMapper.ToResponse(
            template, team?.Name, !await repository.IsInUseAsync(template.Id, cancellationToken));
    }
}
