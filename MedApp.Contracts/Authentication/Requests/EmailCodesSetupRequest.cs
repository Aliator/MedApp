using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>Starts turning on two-factor codes by email: the signed-in account's password.</summary>
public sealed record EmailCodesSetupRequest(string Password)
{
    // Never prints the password.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Password)} = ***");
        return true;
    }
}
