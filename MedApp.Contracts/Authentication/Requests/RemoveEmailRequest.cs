using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Removes the signed-in account's email address, with its password.</summary>
public sealed record RemoveEmailRequest(
    string Password)
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***");
        return true;
    }
}
