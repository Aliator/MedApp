using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Commands.SetLetterTemplateArchived;

/// <summary>
/// Archives a letter template (it can't be chosen for new letters; letters written from it keep their copy) or restores
/// it, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed since).
/// </summary>
public sealed record SetLetterTemplateArchivedCommand(Guid TemplateId, bool IsArchived, int ExpectedVersion)
    : IRequest<LetterTemplateResponse>;
