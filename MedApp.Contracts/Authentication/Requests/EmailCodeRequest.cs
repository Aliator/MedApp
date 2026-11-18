using System.Text;

namespace MedApp.Contracts.Authentication.Requests;

/// <summary>The 6-digit code MedApp emailed, to turn two-factor codes by email on.</summary>
public sealed record EmailCodeRequest(string Code)
{
    // Never prints the code.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Code)} = ***");
        return true;
    }
}
