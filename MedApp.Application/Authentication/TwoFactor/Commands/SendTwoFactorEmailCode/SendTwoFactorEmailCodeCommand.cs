using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;

/// <summary>
/// Emails the signed-in account a code to confirm a change to how it signs in (turning two-factor off, new recovery
/// codes, switching to the app, a new address), when it uses codes by email, given its password. Asking again sends a
/// new code, within the limits on sending again.
/// </summary>
public sealed record SendTwoFactorEmailCodeCommand(
    Guid UserId,
    string Password
) : IRequest<EmailCodeSentResponse>
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Password)} = ***");
        return true;
    }
}
