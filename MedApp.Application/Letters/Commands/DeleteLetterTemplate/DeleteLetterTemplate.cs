using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MediatR;

namespace MedApp.Application.Letters.Commands.DeleteLetterTemplate;

/// <summary>Deletes a letter template no letter was written from (holders of ManageSetup).</summary>
public sealed record DeleteLetterTemplateCommand(Guid TemplateId) : IRequest;

/// <summary>
/// Deletes an unused template (404 if there's none). One a letter was written from is a 409: letters keep a copy of
/// its text, but name it, so it's archived instead. A letter started from it meanwhile is the same 409 (the foreign
/// key refuses the delete).
/// </summary>
public sealed class DeleteLetterTemplateHandler(ILetterTemplateRepository repository) : IRequestHandler<DeleteLetterTemplateCommand>
{
    public static string InUseMessage(string name) =>
        $"Letters have been written from the '{name}' template, so it can't be deleted. Archive it instead.";

    public async Task Handle(DeleteLetterTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetOrThrowAsync(request.TemplateId, cancellationToken);
        if (await repository.IsInUseAsync(template.Id, cancellationToken))
            throw new ConflictException(InUseMessage(template.Name));

        await repository.DeleteAsync(template.Id, cancellationToken);
    }
}
