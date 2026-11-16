using System.Text;
using MedApp.Application.Common.Authentication;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;

/// <summary>
/// The second step of signing in: the challenge from the password step, and a code from the authenticator app
/// (<paramref name="Code"/>) or a recovery code (<paramref name="RecoveryCode"/>), exactly one of them.
/// </summary>
public sealed record LoginTwoFactorCommand(
    string Challenge,
    string? Code,
    string? RecoveryCode,
    string? IpAddress,
    string? UserAgent
) : IRequest<LoginResult>
{
    // Never prints the challenge or the codes.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Challenge)} = ***, {nameof(Code)} = ***, {nameof(RecoveryCode)} = ***");
        return true;
    }
}
