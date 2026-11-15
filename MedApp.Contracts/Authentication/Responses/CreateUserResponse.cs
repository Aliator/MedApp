using System.Text;

namespace MedApp.Contracts.Authentication.Responses;

/// <summary>
/// A new account and the first password the server generated for it, which works until the user changes it. It's
/// returned only this once, for the admin to pass on securely.
/// </summary>
public sealed record CreateUserResponse(
    string Username,
    string Password)
{
    // Never prints the password, so logging this response can't leak it.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Username)} = {Username}, {nameof(Password)} = ***");
        return true;
    }
}
