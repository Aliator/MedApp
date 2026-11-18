using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterTemplateById;

/// <summary>One letter template, archived or not; 404 if there's none.</summary>
public sealed record GetLetterTemplateByIdQuery(Guid TemplateId) : IRequest<LetterTemplateResponse>;
