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

    public static IdentityError OwnRoles =>
        new()
        {
            Code = OwnRolesCode,
            Description = "You can't change your own roles."
        };

    public static IdentityError PasswordIncorrect =>
        new()
        {
            Code = PasswordIncorrectCode,
            Description = "Your password is incorrect."
        };

    public static IdentityError LockedOut =>
        new()
        {
            Code = LockedOutCode,
            Description = "Too many incorrect passwords. Please try again later."
        };

    /// <summary>The user has Booked appointments still to come, which would be left with nobody.</summary>
    public static IdentityError HasUpcomingAppointments(string username, int count) =>
        new()
        {
            Code = HasUpcomingAppointmentsCode,
            Description = $"{username} has {count} upcoming appointment{(count == 1 ? "" : "s")}. Move or cancel them first."
        };

    public const string UserNotFoundCode = "UserNotFound";
    public const string OldPasswordIncorrectCode = "OldPasswordIncorrect";
    public const string RoleNotFoundCode = "RoleNotFound";
    public const string OwnRolesCode = "OwnRoles";
    public const string PasswordIncorrectCode = "PasswordIncorrect";
    public const string LockedOutCode = "LockedOut";
    public const string HasUpcomingAppointmentsCode = "HasUpcomingAppointments";
}