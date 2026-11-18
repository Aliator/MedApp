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
using MedApp.Application.Authentication.Sessions.Commands.ResendSignInCode;
using MedApp.Application.Authentication.Sessions.Queries.GetAuthFeatures;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginAuthenticatorSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.BeginEmailCodesSetup;
using MedApp.Application.Authentication.TwoFactor.Commands.DisableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableEmailCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.EnableTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.RegenerateRecoveryCodes;
using MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;
using MedApp.Application.Authentication.TwoFactor.Commands.SendTwoFactorEmailCode;
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
/// set-up (an authenticator app or codes by email), and the admin settings for sign-in. Everything here that carries a
/// secret (a key, recovery codes) is sent with <c>no-store</c>, as every <c>/api</c> response is. Emailed codes are never
/// in a response, only where they went, masked.
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
    /// <c>POST api/auth/login</c> (a 202) with a code (from the authenticator app, or the one emailed for this sign-in)
    /// or a recovery code. A right one starts the session,
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
    [AllowAnonymous]
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

    /// <summary>
    /// Emails a new code for a sign-in's second step, for an account using codes by email (the new code replaces the
    /// last): the challenge from <c>POST api/auth/login</c>. At most once a minute and three times a sign-in, and within
    /// the account's limit on emailed codes: a 429 with <c>Retry-After</c> otherwise. A challenge that can't be used is a
    /// 401 with <c>code: "sign_in_expired"</c>; an account that doesn't use codes by email a 409. Throttled per client
    /// address as sign-in is.
    /// </summary>
    [HttpPost("login/two-factor/resend")]
    [Tags("Login")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ProducesResponseType(typeof(EmailCodeSentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [AllowAnonymous]
    public async Task<IActionResult> ResendSignInCode(ResendSignInCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ResendSignInCodeCommand(request.Challenge ?? string.Empty), ct));

    /// <summary>What sign-in offers here, for the pages before signing in (whether email with links can be sent).</summary>
    [HttpGet("features")]
    [Tags("Login")]
    [ProducesResponseType(typeof(AuthFeaturesResponse), StatusCodes.Status200OK)]
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [AllowAnonymous]
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
    [Tags("Account")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [Authorize]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmEmailCommand(request.UserId, request.Token ?? string.Empty, User.GetUserId()), ct);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    /// <summary>The signed-in account's email address, and any waiting to be confirmed.</summary>
    [HttpGet("me/email")]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailResponse), StatusCodes.Status200OK)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetMyEmail(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyEmailQuery(User.GetUserId()), ct));

    /// <summary>
    /// Asks to use a new email address, with the password (a wrong one is a 400 on <c>Password</c> and counts towards the
    /// lockout): it's sent a link to confirm it, the confirmed address (if any) is told, and until the link is opened the
    /// old one stays in use (a 202, with the addresses as they now are). While the account signs in with codes by email, a
    /// code too (<c>Code</c>: one emailed by <c>POST api/auth/me/two-factor/email/code</c>, or a recovery code; a missing
    /// or wrong one is a 400 on <c>Code</c>, and wrong ones count towards the lockout). A 409 where email can't be sent; a
    /// 429 after three requests in 15 minutes.
    /// </summary>
    [HttpPut("me/email")]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> ChangeMyEmail(ChangeEmailRequest request, CancellationToken ct) =>
        Accepted(await mediator.Send(
            new ChangeMyEmailCommand(User.GetUserId(), request.Email ?? string.Empty, request.Password ?? string.Empty, request.Code), ct));

    /// <summary>
    /// Removes the signed-in account's email address, and any waiting to be confirmed, with the password. A 409 while the
    /// account signs in with codes by email (turn two-factor off, or switch to the app, first).
    /// </summary>
    [HttpDelete("me/email")]
    [Tags("Account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> RemoveMyEmail([FromBody] RemoveEmailRequest request, CancellationToken ct)
    {
        await mediator.Send(new RemoveMyEmailCommand(User.GetUserId(), request.Password ?? string.Empty), ct);
        return NoContent();
    }

    /// <summary>
    /// Removes another account's email address and any pending one (for example one put on it without its owner), so
    /// "Forgot your password?" can't send a link there. Recorded with the admin who did it; the removed address is told.
    /// A 404 for an unknown account; a 409 while it signs in with codes by email (reset its two-factor first).
    /// </summary>
    [HttpDelete("users/{username}/email")]
    [Tags("Authentication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageUsers)]
    public async Task<IActionResult> RemoveUserEmail(string username, CancellationToken ct)
    {
        var result = await mediator.Send(
            new RemoveUserEmailCommand(username, User.GetUserId(), User.Identity?.Name ?? string.Empty), ct);
        return result.Succeeded ? NoContent() : NotFound();
    }

    /// <summary>
    /// Whether the signed-in account uses two-factor authentication and how (<c>Authenticator</c> or <c>Email</c>), its
    /// recovery codes left, whether it must, and whether it could use codes by email (and why not).
    /// </summary>
    [HttpGet("me/two-factor")]
    [Tags("Account")]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetMyTwoFactor(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyTwoFactorStatusQuery(User.GetUserId()), ct));

    /// <summary>
    /// Starts setting up an authenticator app: a new key, as a QR code and as text. Two-factor stays off (or on with codes
    /// by email, which the app then replaces) until a code confirms it. A 409 while it's on with the app.
    /// </summary>
    [HttpPost("me/two-factor/authenticator")]
    [Tags("Account")]
    [ProducesResponseType(typeof(AuthenticatorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> BeginAuthenticatorSetup(CancellationToken ct) =>
        Ok(await mediator.Send(new BeginAuthenticatorSetupCommand(User.GetUserId()), ct));

    /// <summary>
    /// Turns two-factor on, given the password and a code from the app just set up, and returns the recovery codes once.
    /// The account's other sessions end. A wrong password or code is a 400 on its field. Switching from codes by email
    /// also needs <c>CurrentCode</c> (an emailed code or a recovery code: a 400 on it when missing or wrong, counting
    /// towards the lockout, a 423).
    /// </summary>
    [HttpPost("me/two-factor/enable")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EnableTwoFactorCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty, User.GetSessionId(), request.CurrentCode), ct));

    /// <summary>
    /// Starts turning on two-factor codes by email, given the password: a 6-digit code is emailed to the account's
    /// confirmed address (a 202, saying where, masked, and when it can be sent again); asking again sends a new one, at
    /// most once a minute and three times. A wrong password is a 400 on <c>Password</c> (counting towards the lockout, a
    /// 423); a 409 while two-factor is on, without a confirmed address, or where email can't be sent; a 429 when sending
    /// again is too soon or too often.
    /// </summary>
    [HttpPost("me/two-factor/email")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailCodeSentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> BeginEmailCodesSetup(EmailCodesSetupRequest request, CancellationToken ct) =>
        Accepted(await mediator.Send(new BeginEmailCodesSetupCommand(User.GetUserId(), request.Password ?? string.Empty), ct));

    /// <summary>
    /// Turns two-factor on with codes by email, given the code emailed to set it up, and returns the recovery codes once.
    /// The account's other sessions end. A wrong code is a 400 on <c>Code</c> (a code allows five tries); a 409 while
    /// two-factor is on; a 429 after too many codes for the account.
    /// </summary>
    [HttpPost("me/two-factor/email/enable")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> EnableEmailCodes(EmailCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EnableEmailCodesCommand(User.GetUserId(), request.Code ?? string.Empty, User.GetSessionId()), ct));

    /// <summary>
    /// Emails a code to confirm a change to how an account using codes by email signs in (turning two-factor off, new
    /// recovery codes, switching to the app, a new address), given the password (a 202, saying where, masked). A wrong
    /// password is a 400 on <c>Password</c> (counting towards the lockout, a 423); a 409 for any other account or when the
    /// email can't go now; a 429 when sending again is too soon or too often. A held admin may call it, to switch to the app.
    /// </summary>
    [HttpPost("me/two-factor/email/code")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(EmailCodeSentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> SendTwoFactorEmailCode(SendEmailCodeRequest request, CancellationToken ct) =>
        Accepted(await mediator.Send(new SendTwoFactorEmailCodeCommand(User.GetUserId(), request.Password ?? string.Empty), ct));

    /// <summary>
    /// Turns two-factor off, given the password and a current code (from the app or, for codes by email, the one emailed
    /// to confirm it) or a recovery code. The account's other sessions
    /// end. A 409 for an account that must use it; wrong details are a 400, counting towards the lockout (a 423).
    /// </summary>
    [HttpPost("me/two-factor/disable")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
    {
        await mediator.Send(new DisableTwoFactorCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty, User.GetSessionId()), ct);
        return NoContent();
    }

    /// <summary>
    /// Replaces the recovery codes with a new set, returned once, given the password and a current code from the app
    /// or, for codes by email, the one emailed to confirm it (wrong ones are a 400 and count towards the lockout; codes
    /// count towards the account's limit, a 429).
    /// </summary>
    [HttpPost("me/two-factor/recovery-codes")]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [Tags("Account")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> RegenerateRecoveryCodes(TwoFactorCodeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RegenerateRecoveryCodesCommand(
            User.GetUserId(), request.Password ?? string.Empty, request.Code ?? string.Empty), ct));

    /// <summary>
    /// Turns off a user's two-factor authentication (their key and recovery codes too), for someone who has lost their
    /// app, and signs them out everywhere. Recorded with the admin who did it, and the user is told by email. A 404 for
    /// an unknown user, and a 400 for the admin's own account.
    /// </summary>
    [HttpPost("users/{username}/reset-two-factor")]
    [Tags("Authentication")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ManageUsers)]
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
    [Tags("Admin settings")]
    [ProducesResponseType(typeof(SecuritySettingsResponse), StatusCodes.Status200OK)]
    [RequirePermission(Permission.ManageSecuritySettings)]
    public async Task<IActionResult> GetSecuritySettings(CancellationToken ct) =>
        Ok(await mediator.Send(new GetSecuritySettingsQuery(), ct));

    /// <summary>
    /// Sets the deployment's sign-in rules. Requiring two-factor for admins is a 409 until the admin turning it on uses
    /// it; admins without it are then held to set it up.
    /// </summary>
    [HttpPut("security-settings")]
    [Tags("Admin settings")]
    [ProducesResponseType(typeof(SecuritySettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSecuritySettings)]
    public async Task<IActionResult> SaveSecuritySettings(SaveSecuritySettingsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new SaveSecuritySettingsCommand(request.RequireTwoFactorForAdmins, User.GetUserId(), User.Identity?.Name ?? string.Empty),
            ct));
}
