using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Contracts.Authentication.Responses;

public sealed record ResetUserPasswordResponse(
    string? GeneratedPassword,
    IEnumerable<IdentityError> Errors)
{
    // Never prints the password, so logging this response can't leak it.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(GeneratedPassword)} = {(GeneratedPassword is null ? "null" : "***")}, {nameof(Errors)} = {Errors}");
        return true;
    }
}
