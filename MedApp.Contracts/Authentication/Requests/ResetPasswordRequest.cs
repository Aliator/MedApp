using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>A new password, with the account and token from an emailed reset link.</summary>
public sealed record ResetPasswordRequest(
    Guid UserId,
    string Token,
    string NewPassword)
{
    // Never prints the token or the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(UserId)} = {UserId}, {nameof(Token)} = ***, {nameof(NewPassword)} = ***");
        return true;
    }
}
