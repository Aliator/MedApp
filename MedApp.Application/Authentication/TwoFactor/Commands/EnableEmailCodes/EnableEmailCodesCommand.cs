using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;

/// <summary>
/// Turns two-factor authentication on with codes by email for the signed-in account, with the code
/// <c>BeginEmailCodesSetupCommand</c> emailed (which proves the mailbox is theirs). Returns the recovery codes, once. The
/// account's other sessions end; <paramref name="CurrentSessionId"/> carries on.
/// </summary>
public sealed record EnableEmailCodesCommand(
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
