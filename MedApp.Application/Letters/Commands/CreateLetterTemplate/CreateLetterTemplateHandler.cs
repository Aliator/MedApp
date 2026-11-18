using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.CreateLetterTemplate;

/// <summary>
/// Adds a letter template. Its specialty must exist and not be archived (404, 409); its name must be free in that
/// specialty (409).
/// </summary>
public sealed class CreateLetterTemplateHandler(
    ILetterTemplateRepository repository,
    ISpecialtyRepository specialties,
    TimeProvider timeProvider) : IRequestHandler<CreateLetterTemplateCommand, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(CreateLetterTemplateCommand request, CancellationToken cancellationToken)
    {
        string? specialtyName = null;
        if (request.SpecialtyId is { } specialtyId)
        {
            var chosen = await SpecialtyRules.CheckChosenAsync(
                specialties, [specialtyId], new HashSet<Guid>(), cancellationToken);
            specialtyName = chosen[specialtyId].Name;
        }

        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, request.SpecialtyId, null, cancellationToken))
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(name, specialtyName));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var template = new LetterTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = PathwayEnums.Parse<LetterKind>(request.Kind),
            SpecialtyId = request.SpecialtyId,
            Letterhead = LetterTemplateRules.Tidy(request.Letterhead),
            BodySkeleton = LetterTemplateRules.Tidy(request.BodySkeleton),
            Closing = LetterTemplateRules.Tidy(request.Closing),
            Version = 1,
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddAsync(template, cancellationToken);
        return LetterTemplateMapper.ToResponse(template, specialtyName);
    }
}
