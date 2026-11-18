using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Commands.SetLetterTemplateArchived;

public sealed class SetLetterTemplateArchivedHandler(
    ILetterTemplateRepository repository,
    ISpecialtyRepository specialties,
    TimeProvider timeProvider) : IRequestHandler<SetLetterTemplateArchivedCommand, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(SetLetterTemplateArchivedCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        LetterTemplateRules.EnsureVersion(template, request.ExpectedVersion);

        if (template.IsArchived != request.IsArchived)
        {
            var originalVersion = template.Version;
            template.IsArchived = request.IsArchived;
            template.Version++;
            template.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(template, originalVersion, cancellationToken);
        }

        var specialty = template.SpecialtyId is { } id ? await specialties.GetAsync(id, cancellationToken) : null;
        return LetterTemplateMapper.ToResponse(
            template, specialty?.Name, !await repository.IsInUseAsync(template.Id, cancellationToken));
    }
}
