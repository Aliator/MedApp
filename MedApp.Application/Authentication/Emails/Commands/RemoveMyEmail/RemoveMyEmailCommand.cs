using System.Text;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;

/// <summary>Removes the signed-in account's email address, and any waiting to be confirmed, with its password.</summary>
public sealed record RemoveMyEmailCommand(
    Guid UserId,
    string Password
) : IRequest
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Password)} = ***");
        return true;
    }
}
