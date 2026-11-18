using System.Security.Claims;
using MedApp.API.Common.Authentication;
using MedApp.API.Common.RateLimiting;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.Application.Authentication.Roles.Queries.GetAllRoles;
using MedApp.Application.Authentication.Roles.Queries.GetUserRoles;
using MedApp.Application.Authentication.Sessions.Commands.ExtendSession;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Queries.GetSession;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.Application.Authentication.Settings.Queries.GetMySettings;
using MedApp.Application.Authentication.Sessions.Commands.Logout;
using MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Authentication.Users.Queries.GetAllUsers;
using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Authentication.Users.Queries.GetUserByUsername;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IMediator mediator,
    ISessionCookieService cookieService)
    : ControllerBase
{
    /// <summary>
    /// Signs in. Throttled per client address and per username (<see cref="LoginThrottling"/>): too many attempts is
    /// a 429 with <c>Retry-After</c>, before the password is checked. For an account with two-factor authentication, a
    /// 202 with the challenge for the second step, and, for codes by email, where the code was emailed (masked); a 429
    /// too when the account has had too many codes emailed.
    /// </summary>
    [HttpPost("login")]
    [Tags("Login")]
    // A username and a password: anything bigger is refused (413) before it's read.
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting(LoginThrottling.Policy)]
    [ThrottleLoginByUsername]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(TwoFactorChallengeResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var outcome = await mediator.Send(
            new LoginCommand(
                request.Username,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()));

        // Two-factor authentication: no session (and no cookie) until the second step, with a code, succeeds.
        if (outcome.NeedsTwoFactor)
        {
            NoStore();
            return Accepted(new TwoFactorChallengeResponse(
                outcome.Challenge,
                outcome.ChallengeExpiresAtUtc.Value,
                outcome.Method.ToString(),
                outcome.EmailCode));
        }

        var session = outcome.Session;
        cookieService.AppendSessionCookie(Response, session);

        return Ok(new LoginResponse(
            session.SessionId,
            session.ExpiresAtUtc));
    }
    
    [HttpPost("logout")]
    [Tags("Login")]
    public async Task<IActionResult> Logout()
    {
        await mediator.Send(
            new LogoutCommand(CurrentSessionId()),
            HttpContext.RequestAborted);

        cookieService.DeleteSessionCookie(Response);

        return NoContent();
    }

    /// <summary>
    /// When the signed-in session ends, for the warning before it times out. Reading it doesn't extend it. A
    /// session that has ended is a 401, as for any other request.
    /// </summary>
    [HttpGet("session")]
    [Authorize]
    [Tags("Login")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSession(CancellationToken ct)
    {
        if (CurrentSessionId() is not { } sessionId)
            return Unauthorized();

        NoStore();
        return Ok(await mediator.Send(new GetSessionQuery(sessionId), ct));
    }

    /// <summary>
    /// "Stay signed in": extends the signed-in session to a full session length from now (8 hours), but never past
    /// 24 hours from sign-in, and sends its cookie again with the new expiry.
    /// </summary>
    [HttpPost("session/extend")]
    [Authorize]
    [Tags("Login")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExtendSession(CancellationToken ct)
    {
        if (CurrentSessionId() is not { } sessionId)
            return Unauthorized();

        var session = await mediator.Send(new ExtendSessionCommand(sessionId), ct);

        // The cookie expires with the session, so it's sent again with the new expiry, or the browser would drop it
        // at the old one.
        cookieService.AppendSessionCookie(Response, new LoginResult(sessionId, session.ExpiresAtUtc));

        NoStore();
        return Ok(session);
    }


    /// <summary>
    /// Creates an account with a password the server generates. The password is returned only in this response,
    /// for the admin to pass on securely. An optional email address is sent a link to confirm it (never the password).
    /// </summary>
    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var created = await mediator.Send(
            new CreateUserCommand(request.Username, request.Email, User.GetUserId(), User.Identity?.Name));

        if (!created.Succeeded)
            return BadRequest(created.Result.Errors);

        NoStore();
        return Ok(new CreateUserResponse(request.Username, created.GeneratedPassword));
    }
    
    /// <summary>
    /// One page of users, filtered and sorted on the server. Used by MedApp.Web; the Blazor client still loads
    /// every user from <c>GET api/auth/users</c>. The literal route wins over <c>users/{username}</c>.
    /// </summary>
    [HttpGet("users/search")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchUsers([FromQuery] SearchUsersRequest request, CancellationToken ct)
    {
        var query = new SearchUsersQuery(
            request.Page,
            request.PageSize,
            request.Sort,
            request.Ascending,
            request.Username,
            request.Role);

        return Ok(await mediator.Send(query, ct));
    }

    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await mediator.Send(new GetAllUsersQuery());
        return Ok(users);
    }

    [HttpGet("users/{username}")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserByUsername(string username)
    {
        var user = await mediator.Send(new GetUserByUsernameQuery(username));
        if (user is null)
            return NotFound();

        return Ok(user);
    }
    
    // Any staff member (either role), and only for themselves: both policies must pass.
    [HttpPut("users/{username}/update-password")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Authorize(Policy = AuthPolicies.SelfUserOnly)]
    [Tags("Authentication")]
    public async Task<IActionResult> UpdateUserPassword(
        string username,
        UpdateUserPasswordRequest request)
    {
        var result = await mediator.Send(
            new UpdateUserPasswordCommand(
                username,
                request.OldPassword,
                request.NewPassword,
                User.GetSessionId()));

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }
    
    [HttpPut("users/{username}/reset-password")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [Tags("Authentication")]
    public async Task<IActionResult> ResetUserPassword(string username)
    {
        var response = await mediator.Send(
            new ResetUserPasswordCommand(username));

        if (response.Errors.Any())
            return BadRequest(response.Errors);

        NoStore();
        return Ok(response.GeneratedPassword);
    }
    
    /// <summary>
    /// Signs a user out everywhere: every session they have ends, on every device, and they must sign in again. An
    /// unknown user is a 404.
    /// </summary>
    [HttpPost("users/{username}/sign-out-everywhere")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Tags("Authentication")]
    public async Task<IActionResult> SignOutEverywhere(string username, CancellationToken ct)
    {
        var found = await mediator.Send(new SignOutEverywhereCommand(username), ct);
        return found ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes a user. The signed-in admin confirms with their own password: a wrong one is a 400 (and counts
    /// towards lockout), an unknown user a 404. A user with upcoming booked appointments is a 400
    /// (<c>HasUpcomingAppointments</c>) until they're moved or cancelled.
    /// </summary>
    [HttpDelete("users/{username}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Tags("Authentication")]
    public async Task<IActionResult> DeleteUser(string username, [FromBody] DeleteUserRequest request)
    {
        var result = await mediator.Send(
            new DeleteUserCommand(username, User.Identity?.Name ?? string.Empty, request.Password ?? string.Empty));

        if (result.Succeeded)
            return NoContent();

        if (result.Errors.Any(e => e.Code == IdentityErrors.UserNotFoundCode))
            return NotFound();

        return BadRequest(result.Errors);
    }

    [HttpGet("roles")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await mediator.Send(new GetAllRolesQuery());
        return Ok(roles);
    }
    
    [HttpGet("users/{username}/roles")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(string username)
    {
        var roles = await mediator.Send(
            new GetUserRolesQuery(username));

        return Ok(roles);
    }
    
    [HttpPost("roles")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request)
    {
        var role = await mediator.Send(
            new CreateRoleCommand(request.Name));

        if (role is null)
            return BadRequest();

        return NoContent();
    }

    [HttpPost("roles/assign")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request)
    {
        var role = await mediator.Send(
            new AssignRoleCommand(request.Username, request.Role));

        if (role is null)
            return BadRequest();

        return NoContent();
    }
    
    /// <summary>
    /// Sets a user's roles to exactly the ones given, in one step: any they have that aren't listed are removed.
    /// Admins can't change their own roles (400), so they can't lock themselves out.
    /// </summary>
    [HttpPut("users/{username}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(IEnumerable<IdentityError>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Tags("Roles")]
    public async Task<IActionResult> SetUserRoles(string username, SetUserRolesRequest request)
    {
        var result = await mediator.Send(
            new SetUserRolesCommand(username, request.Roles ?? [], User.Identity?.Name ?? string.Empty));

        if (result.Succeeded)
            return NoContent();

        if (result.Errors.Any(e => e.Code == IdentityErrors.UserNotFoundCode))
            return NotFound();

        return BadRequest(result.Errors);
    }

    [HttpPost("roles/revoke")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    public async Task<IActionResult> RevokeRole(RevokeRoleRequest request)
    {
        var role = await mediator.Send(
            new RevokeRoleCommand(request.Username, request.Role));

        if (role is null)
            return BadRequest();

        return NoContent();
    }

    
    [HttpDelete("roles/{roleName}")]
    [Authorize(Roles = "Admin")]
    [Tags("Roles")]
    public async Task<IActionResult> DeleteRole(string roleName)
    {
        var result = await mediator.Send(new DeleteRoleCommand(roleName));

        if (!result.Succeeded)
            return NotFound();

        return NoContent();
    }
    
    /// <summary>The signed-in user's app settings (a JSON object), or null for none saved yet.</summary>
    [HttpGet("me/settings")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Settings")]
    [ProducesResponseType(typeof(UserSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySettings(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMySettingsQuery(User.GetUserId()), ct));

    /// <summary>Saves the signed-in user's app settings, replacing any they had.</summary>
    [HttpPut("me/settings")]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Settings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveMySettings(SaveSettingsRequest request, CancellationToken ct)
    {
        await mediator.Send(new SaveMySettingsCommand(User.GetUserId(), request.Settings ?? string.Empty), ct);
        return NoContent();
    }

    [HttpGet("whoami")]
    [Tags("Login")]
    [ProducesResponseType(typeof(WhoAmIResponse), StatusCodes.Status200OK)]
    public IActionResult WhoAmI()
    {
        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
        
        return Ok(new WhoAmIResponse(
            User.Identity?.IsAuthenticated ?? false,
            User.Identity?.Name,
            roles,
            User.MustChangePassword(),
            User.MustSetUpTwoFactor()));
    }

    /// <summary>
    /// Tells browsers and proxies not to keep this response: it carries a generated password, shown to the admin once,
    /// or a session's expiry, which is only true for now.
    /// </summary>
    private void NoStore() => Response.Headers.CacheControl = "no-store";

    /// <summary>The session in the request's cookie, or null when there's none, or it isn't a session ID.</summary>
    private Guid? CurrentSessionId() =>
        cookieService.TryGetSessionCookie(Request, out var sessionId) ? sessionId : null;
}