using MedApp.API.Common.Authentication;
using MedApp.API.Common.RateLimiting;
using MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;
using MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail;
using MedApp.Application.Authentication.Emails.Queries.GetMyEmail;
using MedApp.Application.Authentication.Passwords.Commands.ForgotPassword;
using MedApp.Application.Authentication.Passwords.Commands.ResetPassword;
using MedApp.Application.Authentication.Security.Commands.SaveSecuritySettings;
using MedApp.Application.Authentication.Security.Queries.GetSecuritySettings;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Queries.GetMyTwoFactorStatus;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedApp.API.Controllers;

/// <summary>
/// Signing in with two-factor authentication, resetting a forgotten password, staff email addresses, two-factor
/// set-up, and the admin settings for sign-in. Everything here that carries a secret (a key, recovery codes) is sent
/// with <c>no-store</c>, as every <c>/api</c> response is.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AccountSecurityController(
    IMediator mediator,
    ISessionCookieService cookieService)
    : ControllerBase
{
    /// <summary>
    /// The second step of signing in to an account with two-factor authentication: the challenge from
    /// <c>POST api/auth/login</c> (a 202) with an authenticator code or a recovery code. A right one starts the session,
    /// as a password alone does for other accounts (the same cookie and response). A wrong code is a 401 (and counts
    /// towards the lockout, a 423 once it's reached); a challenge that has expired, been used or had too many wrong
    /// codes is a 401 with <c>code: "sign_in_expired"</c>, to sign in again. Throttled per client address as sign-in is.
    /// </summary>
    [HttpPost("login/two-factor")]
    [Tags("Login")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginTwoFactor(LoginTwoFactorRequest request)
    {
        var session = await mediator.Send(new LoginTwoFactorCommand(
            request.Challenge,
            request.Code,
            request.RecoveryCode,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString()));

        cookieService.AppendSessionCookie(Response, session);
        return Ok(new LoginResponse(session.SessionId, session.ExpiresAtUtc));
    }

    /// <summary>What sign-in offers here, for the pages before signing in (whether email with links can be sent).</summary>
    [HttpGet("features")]
    [Tags("Login")]
    [ProducesResponseType(typeof(AuthFeaturesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatures(CancellationToken ct) =>
        Ok(await mediator.Send(new GetAuthFeaturesQuery(), ct));

    /// <summary>
    /// "Forgot your password?": emails a reset link to the account with this username, or to every account whose
    /// confirmed address it is. Always a 202, whether or not anything matched, so it can't be used to learn which
    /// accounts or addresses exist. Throttled per client address (as sign-in is) and per username or address.
    /// </summary>
    [HttpPost("password/forgot")]
    [Tags("Login")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ThrottleResetRequests]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ForgotPasswordCommand(request.UsernameOrEmail ?? string.Empty), ct);
        return Accepted();
    }

    /// <summary>
    /// Chooses a new password with an emailed reset link's account and token. A wrong, used or expired link is a 400
    /// with the code <c>InvalidToken</c>; a password the rules refuse, or the current one (<c>SamePassword</c>), a 400
    /// with Identity's errors. On success every session the account had ends.
    /// </summary>
    [HttpPost("password/reset")]
    [Tags("Login")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ResetPasswordCommand(request.UserId, request.Token ?? string.Empty, request.NewPassword ?? string.Empty), ct);

        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    /// <summary>
    /// Confirms an email address with the account and token from the link sent to it, signed in as that account: the
    /// link proves the mailbox, the session the person (a mistyped or shared address can't be confirmed by whoever reads
    /// it). Signed out is a 401; a wrong, used or expired link a 400 with the code <c>InvalidToken</c>; a link for
    /// another account a 400 with <c>WrongAccount</c>.
    /// </summary>
    [HttpPost("email/confirm")]
    [Authorize]
    [Tags("Account")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmEmailCommand(request.UserId, request.Token ?? string.Empty, User.GetUserId()), ct);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    /// <summary>The signed-in account's email address, and any waiting to be confirmed.</summary>
    [HttpGet("me/email")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyEmail(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyEmailQuery(User.GetUserId()), ct));

    /// <summary>
    /// Asks to use a new email address, with the password (a wrong one is a 400 on <c>Password</c> and counts towards the
    /// lockout): it's sent a link to confirm it, the confirmed address (if any) is told, and until the link is opened the
    /// old one stays in use (a 202, with the addresses as they now are). A 409 where email can't be sent; a 429 after
    /// three requests in 15 minutes.
    /// </summary>
    [HttpPut("me/email")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangeMyEmail(ChangeEmailRequest request, CancellationToken ct) =>
        Accepted(await mediator.Send(
            new ChangeMyEmailCommand(User.GetUserId(), request.Email ?? string.Empty, request.Password ?? string.Empty), ct));

    /// <summary>Removes the signed-in account's email address, and any waiting to be confirmed, with the password.</summary>
    [HttpDelete("me/email")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> RemoveMyEmail([FromBody] RemoveEmailRequest request, CancellationToken ct)
    {
        await mediator.Send(new RemoveMyEmailCommand(User.GetUserId(), request.Password ?? string.Empty), ct);
        return NoContent();
    }

    /// <summary>
    /// Removes another account's email address and any pending one (for example one put on it without its owner), so
    /// "Forgot your password?" can't send a link there. Recorded with the admin who did it; the removed address is told.
    /// A 404 for an unknown account.
    /// </summary>
    [HttpDelete("users/{username}/email")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserEmail(string username, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RemoveUserEmailCommand(username, User.GetUserId(), User.Identity?.Name ?? string.Empty), ct);
        return result.Succeeded ? NoContent() : NotFound();
    }

    /// <summary>Whether the signed-in account uses two-factor authentication, its recovery codes left, and whether it must.</summary>
    [HttpGet("me/two-factor")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Account")]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTwoFactor(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyTwoFactorStatusQuery(User.GetUserId()), ct));

    /// <summary>
    /// Starts setting up an authenticator app: a new key, as a QR code and as text. Two-factor stays off until a code
    /// confirms it. A 409 while it's on.
    /// </summary>
    [HttpPost("me/two-factor/authenticator")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Account")]
    [ProducesResponseType(typeof(AuthenticatorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BeginAuthenticatorSetup(CancellationToken ct) =>
        Ok(await mediator.Send(new BeginAuthenticatorSetupCommand(User.GetUserId()), ct));

    /// <summary>
    /// Turns two-factor on, given the password and a code from the app just set up, and returns the recovery codes once.
    /// The account's other sessions end. A wrong password or code is a 400 on its field.
    /// </summary>
    [HttpPost("me/two-factor/enable")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EnableTwoFactorCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty, User.GetSessionId()), ct));

    /// <summary>
    /// Turns two-factor off, given the password and a current code (or a recovery code). The account's other sessions
    /// end. A 409 for an account that must use it; wrong details are a 400, counting towards the lockout (a 423).
    /// </summary>
    [HttpPost("me/two-factor/disable")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await mediator.Send(new DisableTwoFactorCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty, User.GetSessionId()), ct);
        return NoContent();
    }

    /// <summary>
    /// Replaces the recovery codes with a new set, returned once, given the password and a current authenticator code
    /// (wrong ones are a 400 and count towards the lockout; codes count towards the account's limit, a 429).
    /// </summary>
    [HttpPost("me/two-factor/recovery-codes")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> RegenerateRecoveryCodes(TwoFactorCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RegenerateRecoveryCodesCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty), ct));

    /// <summary>
    /// Turns off a user's two-factor authentication (their key and recovery codes too), for someone who has lost their
    /// app, and signs them out everywhere. Recorded with the admin who did it, and the user is told by email. A 404 for
    /// an unknown user, and a 400 for the admin's own account.
    /// </summary>
    [HttpPost("users/{username}/reset-two-factor")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserTwoFactor(string username, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ResetUserTwoFactorCommand(username, User.GetUserId(), User.Identity?.Name ?? string.Empty), ct);

        if (result.Succeeded)
            return NoContent();

        if (result.Errors.Any(e => e.Code == IdentityErrors.UserNotFoundCode))
            return NotFound();

        return BadRequest(result.Errors);
    }

    /// <summary>The deployment's sign-in rules (Admin settings).</summary>
    [HttpGet("security-settings")]
    [Authorize(Roles = "Admin")]
    [Tags("Admin settings")]
    [ProducesResponseType(typeof(SecuritySettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecuritySettings(CancellationToken ct) =>
        Ok(await mediator.Send(new GetSecuritySettingsQuery(), ct));

    /// <summary>
    /// Sets the deployment's sign-in rules. Requiring two-factor for admins is a 409 until the admin turning it on uses
    /// it; admins without it are then held to set it up.
    /// </summary>
    [HttpPut("security-settings")]
    [Authorize(Roles = "Admin")]
    [Tags("Admin settings")]
    [ProducesResponseType(typeof(SecuritySettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveSecuritySettings(SaveSecuritySettingsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new SaveSecuritySettingsCommand(request.RequireTwoFactorForAdmins, User.GetUserId(), User.Identity?.Name ?? string.Empty),
            ct));
}
