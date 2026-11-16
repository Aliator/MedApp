namespace MedApp.API.Common.Authentication;

public static class SessionAuthenticationDefaults
{
    public const string Scheme = "Session";
    public const string CookieName = "medapp.session";

    /// <summary>
    /// The claim a session gets (with the value <c>true</c>) while its user must change the password an admin gave
    /// them. <see cref="PasswordChangeRequired"/> refuses their other API requests until they do.
    /// </summary>
    public const string MustChangePasswordClaim = "medapp:must_change_password";
}
