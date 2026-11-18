using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// A new email address for the signed-in account, with its password. It's used once the link sent to it is opened by
/// the same account, signed in. While the account signs in with codes by email, <paramref name="Code"/> too: a code
/// emailed to the current address (<c>POST api/auth/me/two-factor/email/code</c>), or a recovery code.
/// </summary>
public sealed record ChangeEmailRequest(
    string Email,
    string Password,
    string? Code = null)
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***, {nameof(Code)} = ***");
        return true;
    }
}
