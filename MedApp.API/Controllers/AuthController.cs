using System.Security.Claims;
using MedApp.API.Common.Authentication;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Roles.Queries.GetAllRoles;
using MedApp.Application.Authentication.Roles.Queries.GetUserRoles;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Commands.Logout;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
using MedApp.Application.Authentication.Users.Commands.ResetUserPassword;
using MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;
using MedApp.Application.Authentication.Users.Queries.GetAllUsers;
using MedApp.Application.Authentication.Users.Queries.GetUserByUsername;
using MedApp.Application.Common.Authentication;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IMediator mediator,
    ISessionCookieService cookieService)
    : ControllerBase
{
    [HttpPost("login")]
    [Tags("Login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var session = await mediator.Send(
            new LoginCommand(
                request.Username,
                request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString()));

        cookieService.AppendSessionCookie(Response, session);

        return Ok(new LoginResponse(
            session.SessionId,
            session.ExpiresAtUtc));
    }
    
    [HttpPost("logout")]
    [Tags("Login")]
    public async Task<IActionResult> Logout()
    {
        Guid? sessionId = null;

        if (Request.Cookies.TryGetValue(
                SessionAuthenticationDefaults.CookieName,
                out var rawSessionId)
            && Guid.TryParse(rawSessionId, out var parsed))
        {
            sessionId = parsed;
        }

        await mediator.Send(
            new LogoutCommand(sessionId),
            HttpContext.RequestAborted);

        cookieService.DeleteSessionCookie(Response);

        return NoContent();
    }


    [HttpPost("users")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var result = await mediator.Send(
            new CreateUserCommand(request.Username, request.Password));

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
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

        return Ok(new UserResponse(user.UserId, user.Username, user.Roles));
    }
    
    [HttpPut("users/{username}/update-password")]
    [Authorize(Roles = "User", Policy = "SelfUserOnly")]
    [Tags("Authentication")]
    public async Task<IActionResult> UpdateUserPassword(
        string username,
        UpdateUserPasswordRequest request)
    {
        var result = await mediator.Send(
            new UpdateUserPasswordCommand(
                username,
                request.OldPassword,
                request.NewPassword));

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }
    
    [HttpPut("users/{username}/reset-password")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    public async Task<IActionResult> ResetUserPassword(string username)
    {
        var response = await mediator.Send(
            new ResetUserPasswordCommand(username));

        if (response.Errors.Any())
            return BadRequest(response.Errors);

        return Ok(response.GeneratedPassword);
    }
    
    [HttpDelete("users/{username}")]
    [Authorize(Roles = "Admin")]
    [Tags("Authentication")]
    public async Task<IActionResult> DeleteUser(string username)
    {
        var result = await mediator.Send(new DeleteUserCommand(username));

        if (!result.Succeeded)
            return NotFound();

        return NoContent();
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
    
    [HttpGet("whoami")]
    [Tags("Login")]
    public IActionResult WhoAmI()
    {
        var roles = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();
        
        return Ok(new WhoAmIResponse(
            User.Identity?.IsAuthenticated ?? false,
            User.Identity?.Name,
            roles));
    }
}