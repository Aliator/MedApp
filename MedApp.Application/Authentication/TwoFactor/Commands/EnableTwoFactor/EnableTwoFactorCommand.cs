using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;

/// <summary>
/// Turns two-factor authentication on for the signed-in account, once a code from the app it was just set up in
/// proves the app has the key. Returns the account's recovery codes, once. The account's other sessions end;
/// <paramref name="CurrentSessionId"/>, the one it was turned on from, carries on.
/// </summary>
public sealed record EnableTwoFactorCommand(
    Guid UserId,
    string Code,
    Guid? CurrentSessionId
) : IRequest<RecoveryCodesResponse>
{
    // Never prints the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Code)} = ***");
        return true;
    }
}
