using System.Security.Claims;
using System.Text.Encodings.Web;
using MedApp.Application.Common.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.Authentication;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionService sessionService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionAuthenticationDefaults.CookieName, out var rawSessionId))
            return AuthenticateResult.NoResult();

        if (!Guid.TryParse(rawSessionId, out var sessionId))
            return AuthenticateResult.Fail("Invalid session id.");

        var validation = await sessionService.ValidateSessionAsync(
            sessionId,
            Context.RequestAborted);

        if (validation is null)
            return AuthenticateResult.Fail("Session not found or expired.");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, validation.UserId.ToString()),
            new Claim(ClaimTypes.Name, validation.Username)
        };

        foreach (var role in validation.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

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