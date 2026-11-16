using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// Turns two-factor authentication off for the signed-in account: their password, and a current code from their
/// authenticator app or one of their recovery codes.
/// </summary>
public sealed record DisableTwoFactorRequest(
    string Password,
    string Code)
{
    // Never prints the password or the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***, {nameof(Code)} = ***");
        return true;
    }
}
