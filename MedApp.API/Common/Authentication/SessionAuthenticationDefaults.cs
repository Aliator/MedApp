namespace MedApp.API.Common.Authentication;

public static class SessionAuthenticationDefaults
{
    public const string Scheme = "Session";

    /// <summary>
    /// The session cookie's name in Development, where the app is plain http (the Vite dev server), so it can't have
    /// the <c>__Host-</c> prefix, which needs <c>Secure</c>. <see cref="ISessionCookieService.CookieName"/> picks one.
    /// </summary>
    public const string DevelopmentCookieName = "medapp.session";

    /// <summary>
    /// The session cookie's name everywhere else. Browsers accept a <c>__Host-</c> cookie only if it's <c>Secure</c>,
    /// has <c>Path=/</c> and no <c>Domain</c>, so only this host, over https, can have set it: a sibling subdomain or
    /// someone on the network over plain http can't plant or overwrite it (session fixation).
    /// </summary>
    public const string HostCookieName = "__Host-medapp.session";

    /// <summary>
    /// The claim a session gets (with the value <c>true</c>) while its user must change the password an admin gave
    /// them. <see cref="PasswordChangeRequired"/> refuses their other API requests until they do.
    /// </summary>
    public const string MustChangePasswordClaim = "medapp:must_change_password";

    /// <summary>
    /// The claim a session gets (with the value <c>true</c>) while its user is an admin who must use two-factor
    /// authentication and hasn't set it up. <see cref="TwoFactorSetupRequired"/> refuses their other API requests until
    /// they do.
    /// </summary>
    public const string MustSetUpTwoFactorClaim = "medapp:must_set_up_two_factor";

    /// <summary>
    /// The claim that carries the ID of the session the request came with, so a change to the user's own account
    /// (their password) can end their other sessions and keep this one.
    /// </summary>
    public const string SessionIdClaim = "medapp:session_id";

    /// <summary>
    /// One claim per permission the session's roles grant (the permission's name), read with the roles on every request.
    /// Permission policies (<see cref="RequirePermissionAttribute"/>) check these, never role names.
    /// </summary>
    public const string PermissionClaim = "medapp:permission";

    /// <summary>
    /// One claim per team the session's user belongs to (the team's ID), read with the roles on every request: their
    /// team walls (<c>TeamScope</c>, <see cref="ClaimsPrincipalExtensions.ToTeamScope"/>).
    /// </summary>
    public const string TeamClaim = "medapp:team";
}
