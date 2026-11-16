using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;

/// <summary>Confirms a pending email address with the account and token from the link sent to it.</summary>
public sealed record ConfirmEmailCommand(
    Guid UserId,
    string Token
) : IRequest<IdentityResult>
{
    // Never prints the token.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Token)} = ***");
        return true;
    }
}
