using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// The signed-in account's password and a 6-digit code: to turn the authenticator app on (a code from the app just set
/// up), or to replace the recovery codes (from the app or, with codes by email, the one emailed to confirm it). Switching
/// from codes by email to the app needs <paramref name="CurrentCode"/> too: the code emailed to confirm it, or a recovery
/// code.
/// </summary>
public sealed record TwoFactorCodeRequest(
    string Password,
    string Code,
    string? CurrentCode = null)
{
    // Never prints the password or the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***, {nameof(Code)} = ***, {nameof(CurrentCode)} = ***");
        return true;
    }
}
