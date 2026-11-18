using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Commands.CreateLetterTemplate;

/// <summary>Adds a letter template (holders of ManageSetup).</summary>
public sealed record CreateLetterTemplateCommand(
    string Name,
    string Kind,
    Guid? SpecialtyId,
    string Letterhead,
    string BodySkeleton,
    string Closing) : IRequest<LetterTemplateResponse>;
