using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Commands.UpdateLetterTemplate;

/// <summary>
/// Replaces a letter template's details, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it
/// has changed since). Letters already started from it keep their copy of its text (PR 16).
/// </summary>
public sealed record UpdateLetterTemplateCommand(
    Guid TemplateId,
    string Name,
    string Kind,
    Guid? TeamId,
    string Letterhead,
    string BodySkeleton,
    string Closing,
    int ExpectedVersion) : IRequest<LetterTemplateResponse>;
