using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;

/// <summary>
/// Starts turning two-factor codes by email on for the signed-in account, given their password: a code is emailed to
/// the account's confirmed address, which <c>EnableEmailCodesCommand</c> takes to prove the mailbox. Asking again sends
/// a new code, within the limits on sending again.
/// </summary>
public sealed record BeginEmailCodesSetupCommand(
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
