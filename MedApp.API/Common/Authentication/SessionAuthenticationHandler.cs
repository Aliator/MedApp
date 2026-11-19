using System.Security.Claims;
using System.Text.Encodings.Web;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.Authentication;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionService sessionService,
    ISessionCookieService cookies)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!cookies.TryGetSessionCookie(Request, out var sessionId))
            return AuthenticateResult.NoResult();

        if (sessionId is null)
            return AuthenticateResult.Fail("Invalid session id.");

        var validation = await sessionService.ValidateSessionAsync(
            sessionId.Value,
            Context.RequestAborted);

        if (validation is null)
            return AuthenticateResult.Fail("Session not found or expired.");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, validation.UserId.ToString()),
            new Claim(ClaimTypes.Name, validation.Username),
            new Claim(SessionAuthenticationDefaults.SessionIdClaim, sessionId.Value.ToString())
        };

        foreach (var role in validation.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in (validation.Permissions ?? new HashSet<Permission>()).Order())
            claims.Add(new Claim(SessionAuthenticationDefaults.PermissionClaim, permission.ToString()));

        foreach (var teamId in validation.TeamIds ?? [])
            claims.Add(new Claim(SessionAuthenticationDefaults.TeamClaim, teamId.ToString()));

        if (validation.MustChangePassword)
            claims.Add(new Claim(SessionAuthenticationDefaults.MustChangePasswordClaim, "true"));

        if (validation.MustSetUpTwoFactor)
            claims.Add(new Claim(SessionAuthenticationDefaults.MustSetUpTwoFactorClaim, "true"));

        var identity = new ClaimsIdentity(
            claims,
            SessionAuthenticationDefaults.Scheme);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(
            principal,
            SessionAuthenticationDefaults.Scheme);

        return AuthenticateResult.Success(ticket);
    }
}
