using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Asks for a code by email to confirm a change to how the signed-in account signs in: its password.</summary>
public sealed record SendEmailCodeRequest(string Password)
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***");
        return true;
    }
}
