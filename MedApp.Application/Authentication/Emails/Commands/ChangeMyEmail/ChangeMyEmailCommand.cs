using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;

/// <summary>
/// The signed-in account asks to use a new email address. It waits until the link sent to it is opened; until then the
/// old one (if any) stays in use.
/// </summary>
public sealed record ChangeMyEmailCommand(
    Guid UserId,
    string Email
) : IRequest<EmailResponse>;
