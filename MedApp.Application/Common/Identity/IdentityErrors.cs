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

    /// <summary>
    /// An emailed link's token is wrong, used, or expired (or its account is gone): one answer for all, so a link can't
    /// be used to learn whether an account exists. Identity's own code for a bad token.
    /// </summary>
    public static IdentityError InvalidToken =>
        new()
        {
            Code = InvalidTokenCode,
            Description = "This link has expired or has already been used. Ask for a new one."
        };

    public static IdentityError SamePassword =>
        new()
        {
            Code = SamePasswordCode,
            Description = "The new password must be different from the current one."
        };

    public static IdentityError OwnTwoFactor =>
        new()
        {
            Code = OwnTwoFactorCode,
            Description = "You can't reset your own two-factor authentication. Turn it off in Account Settings, Security, or ask another admin."
        };

    /// <summary>An emailed link opened while signed in as another account than the one it's for.</summary>
    public static IdentityError WrongAccount =>
        new()
        {
            Code = WrongAccountCode,
            Description = "This link is for another account. Sign in as that account, then open the link again."
        };

    public const string WrongAccountCode = "WrongAccount";
    public const string UserNotFoundCode = "UserNotFound";
    public const string InvalidTokenCode = "InvalidToken";
    public const string SamePasswordCode = "SamePassword";
    public const string OwnTwoFactorCode = "OwnTwoFactor";
    public const string OldPasswordIncorrectCode = "OldPasswordIncorrect";
    public const string RoleNotFoundCode = "RoleNotFound";
    public const string OwnRolesCode = "OwnRoles";
    public const string PasswordIncorrectCode = "PasswordIncorrect";
    public const string LockedOutCode = "LockedOut";
    public const string HasUpcomingAppointmentsCode = "HasUpcomingAppointments";
}