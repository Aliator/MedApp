using System.Text;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;

/// <summary>
/// Replaces the signed-in account's recovery codes with a new set, shown once, given a current code from their
/// authenticator app. The old codes stop working.
/// </summary>
public sealed record RegenerateRecoveryCodesCommand(
    Guid UserId,
    string Code
) : IRequest<RecoveryCodesResponse>
{
    // Never prints the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Code)} = ***");
        return true;
    }
}
