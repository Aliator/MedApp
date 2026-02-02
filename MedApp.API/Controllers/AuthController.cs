using MedApp.Application.Auth.Commands.AssignRole;
using MedApp.Application.Auth.Commands.CreateRole;
using MedApp.Application.Auth.Commands.CreateUser;
using MedApp.Application.Auth.Commands.Login;
using MedApp.Application.Auth.Queries.GetAllRoles;
using MedApp.Application.Auth.Queries.GetAllUsers;
using MedApp.Application.Auth.Queries.GetUserRoles;
using MedApp.Contracts.Auth.Requests;
using MedApp.Contracts.Auth.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var token = await mediator.Send(
            new LoginCommand(request.Username, request.Password));

        return Ok(new LoginResponse(token));
    }

    [HttpPost("users")]
    [Authorize(Roles = "Test")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var result = await mediator.Send(
            new CreateUserCommand(request.Username, request.Password));

        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return NoContent();
    }


    [HttpPost("roles")]
    [Authorize(Roles = "Test")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request)
    {
        var role = await mediator.Send(
            new CreateRoleCommand(request.Name));

        if (role is null)
            return BadRequest();

        return NoContent();
    }


    [HttpPost("roles/assign")]
    [Authorize(Roles = "Test")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request)
    {
        var role = await mediator.Send(
            new AssignRoleCommand(request.Username, request.Role));

        if (role is null)
            return BadRequest();

        return NoContent();
    }
    
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await mediator.Send(new GetAllUsersQuery());
        return Ok(users);
    }
    
    [HttpGet("roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRoles()
    {
        var roles = await mediator.Send(new GetAllRolesQuery());
        return Ok(roles);
    }
    
    [HttpGet("users/{username}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(string username)
    {
        var roles = await mediator.Send(
            new GetUserRolesQuery(username));

        return Ok(roles);
    }


    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        return Ok(new
        {
            IsAuthenticated = User.Identity?.IsAuthenticated,
            Name = User.Identity?.Name
        });
    }
}