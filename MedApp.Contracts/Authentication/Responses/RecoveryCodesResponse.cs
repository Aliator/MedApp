using System.Text;

namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// The account's new recovery codes, shown once: each signs in once in place of an authenticator code. Sent with
/// <c>no-store</c>.
/// </summary>
public sealed record RecoveryCodesResponse(
    IReadOnlyList<string> RecoveryCodes)
{
    // Never prints the codes.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(RecoveryCodes)} = {RecoveryCodes.Count} codes");
        return true;
    }
}
