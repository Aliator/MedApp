using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterTemplateById;

public sealed class GetLetterTemplateByIdHandler(ILetterTemplateRepository repository, ISpecialtyRepository specialties)
    : IRequestHandler<GetLetterTemplateByIdQuery, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(GetLetterTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        var specialty = template.SpecialtyId is { } id ? await specialties.GetAsync(id, cancellationToken) : null;
        return LetterTemplateMapper.ToResponse(template, specialty?.Name);
    }
}
