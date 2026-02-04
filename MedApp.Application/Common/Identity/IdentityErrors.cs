using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public static class IdentityErrors
{
    public static IdentityError UserNotFound =>
        new()
        {
            Code = UserNotFoundCode,
            Description = "User not found."
        };

    public static IdentityError OldPasswordIncorrect =>
        new()
        {
            Code = OldPasswordIncorrectCode,
            Description = "Old password is incorrect."
        };

    public static IdentityError RoleNotFound =>
        new()
        {
            Code = RoleNotFoundCode,
            Description = "Role not found."
        };

    public const string UserNotFoundCode = "UserNotFound";
    public const string OldPasswordIncorrectCode = "OldPasswordIncorrect";
    public const string RoleNotFoundCode = "RoleNotFound";
}