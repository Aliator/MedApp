using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>
/// A new email address for the signed-in account, with its password. It's used once the link sent to it is opened by
/// the same account, signed in.
/// </summary>
public sealed record ChangeEmailRequest(
    string Email,
    string Password)
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***");
        return true;
    }
}
