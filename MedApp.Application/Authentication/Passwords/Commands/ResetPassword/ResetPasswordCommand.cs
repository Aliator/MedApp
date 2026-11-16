using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Passwords.Commands.ResetPassword;

/// <summary>Sets a new password with the account and token from an emailed reset link.</summary>
public sealed record ResetPasswordCommand(
    Guid UserId,
    string Token,
    string NewPassword
) : IRequest<IdentityResult>
{
    // Never prints the token or the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Token)} = ***, {nameof(NewPassword)} = ***");
        return true;
    }
}
