using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;

/// <summary>
/// The signed-in account asks to use a new email address, with its password (and, while it signs in with codes by email,
/// <paramref name="Code"/>: a code emailed to the current address, or a recovery code). The address waits until the link sent to
/// it is opened, by the same account signed in; until then the old one (if any) stays in use, and is told of the request.
/// </summary>
public sealed record ChangeMyEmailCommand(
    Guid UserId,
    string Email,
    string Password,
    string? Code = null
) : IRequest<EmailResponse>
{
    // Never prints the address or the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Email)} = ***, {nameof(Password)} = ***, {nameof(Code)} = ***");
        return true;
    }
}
