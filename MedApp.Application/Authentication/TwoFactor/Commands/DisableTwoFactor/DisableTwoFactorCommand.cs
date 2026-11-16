using System.Text;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;

/// <summary>
/// Turns two-factor authentication off for the signed-in account, with their password and a current code (from the app,
/// or a recovery code). The account's other sessions end; <paramref name="CurrentSessionId"/> carries on.
/// </summary>
public sealed record DisableTwoFactorCommand(
    Guid UserId,
    string Password,
    string Code,
    Guid? CurrentSessionId
) : IRequest
{
    // Never prints the password or the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Password)} = ***, {nameof(Code)} = ***");
        return true;
    }
}
