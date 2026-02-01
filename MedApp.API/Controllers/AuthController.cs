using MedApp.API.Dtos.Requests;
using MedApp.Application.Auth.Commands.AssignRole;
using MedApp.Application.Auth.Commands.CreateRole;
using MedApp.Application.Auth.Commands.CreateUser;
using MedApp.Application.Auth.Commands.Login;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var token = await mediator.Send(
            new LoginCommand(request.Username, request.Password));

        return Ok(new { accessToken = token });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var id = await mediator.Send(
            new CreateUserCommand(request.Username, request.Password));

        return CreatedAtAction(nameof(CreateUser), new { id }, null);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request)
    {
        await mediator.Send(new CreateRoleCommand(request.Name));
        return NoContent();
    }

    [HttpPost("roles/assign")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request)
    {
        await mediator.Send(
            new AssignRoleCommand(request.Username, request.Role));

        return NoContent();
    }
}