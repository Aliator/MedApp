using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.UpdateLetterTemplate;

/// <summary>
/// Edits a letter template (archived or not), from the caller's version (409 if it changed since). A newly chosen
/// specialty must exist and not be archived (an archived one may stay); the name must be free in its specialty (409).
/// Nothing is saved if nothing changed.
/// </summary>
public sealed class UpdateLetterTemplateHandler(
    ILetterTemplateRepository repository,
    ISpecialtyRepository specialties,
    TimeProvider timeProvider) : IRequestHandler<UpdateLetterTemplateCommand, LetterTemplateResponse>
{
    public async Task<LetterTemplateResponse> Handle(UpdateLetterTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        LetterTemplateRules.EnsureVersion(template, request.ExpectedVersion);

        string? specialtyName = null;
        if (request.SpecialtyId is { } specialtyId)
        {
            var alreadyGiven = template.SpecialtyId is { } current ? new HashSet<Guid> { current } : [];
            var chosen = await SpecialtyRules.CheckChosenAsync(specialties, [specialtyId], alreadyGiven, cancellationToken);
            specialtyName = chosen[specialtyId].Name;
        }

        var name = request.Name.Trim();
        var kind = PathwayEnums.Parse<LetterKind>(request.Kind);
        var letterhead = LetterTemplateRules.Tidy(request.Letterhead);
        var body = LetterTemplateRules.Tidy(request.BodySkeleton);
        var closing = LetterTemplateRules.Tidy(request.Closing);

        if (template.Name == name && template.Kind == kind && template.SpecialtyId == request.SpecialtyId
            && template.Letterhead == letterhead && template.BodySkeleton == body && template.Closing == closing)
            return LetterTemplateMapper.ToResponse(template, specialtyName);

        var nameOrSpecialtyChanged = !string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)
                                     || template.SpecialtyId != request.SpecialtyId;
        if (nameOrSpecialtyChanged
            && await repository.NameExistsAsync(name, request.SpecialtyId, template.Id, cancellationToken))
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(name, specialtyName));

        var originalVersion = template.Version;
        template.Name = name;
        template.Kind = kind;
        template.SpecialtyId = request.SpecialtyId;
        template.Letterhead = letterhead;
        template.BodySkeleton = body;
        template.Closing = closing;
        template.Version++;
        template.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(template, originalVersion, cancellationToken);

        return LetterTemplateMapper.ToResponse(template, specialtyName);
    }
}
