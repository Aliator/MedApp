using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterTemplates;

/// <summary>
/// Every letter template: those for every specialty first, then by specialty name, then by name. Archived ones only when
/// asked for.
/// </summary>
public sealed record GetLetterTemplatesQuery(bool IncludeArchived = false) : IRequest<IReadOnlyList<LetterTemplateResponse>>;
