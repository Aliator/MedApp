using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;

/// <summary>
/// Turns two-factor authentication on for the signed-in account, given their password and once a code from the app it was just set up in
/// proves the app has the key. Returns the account's recovery codes, once. The account's other sessions end;
/// <paramref name="CurrentSessionId"/>, the one it was turned on from, carries on. When the account uses codes by email,
/// switching needs <paramref name="CurrentCode"/> too: the code emailed to confirm it, or a recovery code.
/// </summary>
public sealed record EnableTwoFactorCommand(
    Guid UserId,
    string Password,
    string Code,
    Guid? CurrentSessionId,
    string? CurrentCode = null
) : IRequest<RecoveryCodesResponse>
{
    // Never prints the password or the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Password)} = ***, {nameof(Code)} = ***, {nameof(CurrentCode)} = ***");
        return true;
    }
}
