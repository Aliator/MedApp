using MedApp.API.Common.Authentication;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using MedApp.Application.Authentication.Roles.Commands.DeleteRole;
using MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;
using MedApp.Application.Authentication.Roles.Commands.UpdateRole;
using MedApp.Application.Authentication.Roles.Queries.GetPermissions;
using MedApp.Application.Authentication.Roles.Queries.GetRoleById;
using MedApp.Application.Authentication.Roles.Queries.GetRoles;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Roles.Requests;
using MedApp.Contracts.Roles.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Roles and the permissions they grant (<c>docs/centre-phase2-plan.md</c>, decisions 51 to 68): the Roles and
/// Permissions page. Permissions are fixed in code (<c>api/permissions</c>); roles are data that holders of ManageRoles
/// make, change, archive and delete. The built-in Admin role refuses every change (409); the Administrator-only
/// permissions are refused on any other role (400); a role someone holds is archived, never deleted (409). Every change
/// carries the version the caller saw (<c>expectedVersion</c>: 409 if another admin changed it since), is recorded in
/// the activity log in the same transaction, and is emailed to the other admins. Giving people roles is
/// <c>PUT api/auth/users/{username}/roles</c>.
/// </summary>
[ApiController]
[Route("api")]
[Tags("Roles")]
public sealed class RolesController(IMediator mediator) : ControllerBase
{
    /// <summary>The permission catalogue: each permission's name, group, description, and whether it's for Admin only.</summary>
    [HttpGet("permissions")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(CancellationToken ct) =>
        Ok(await mediator.Send(new GetPermissionsQuery(), ct));

    /// <summary>
    /// Every role, archived ones too (Admin first, then by name), with its permissions and how many hold it. Also for
    /// the Users page's role picker, so it needs ManageUsers (both are for Admin only, so the same people hold them).
    /// </summary>
    [HttpGet("roles")]
    [RequirePermission(Permission.ManageUsers)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken ct) =>
        Ok(await mediator.Send(new GetRolesQuery(), ct));

    /// <summary>One role, with its permissions and who holds it.</summary>
    [HttpGet("roles/{id:guid}")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRole(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetRoleByIdQuery(id), ct));

    [HttpPost("roles")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var role = await mediator.Send(
            new CreateRoleCommand(request.Name, request.Description, request.Permissions ?? [], User.GetUserId(), ActorUsername),
            ct);
        return Created($"/api/roles/{role.Id}", role);
    }

    /// <summary>
    /// Sets a role's name, description and exactly its permissions. Everyone who holds it has the new permissions from
    /// their next request.
    /// </summary>
    [HttpPut("roles/{id:guid}")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new UpdateRoleCommand(id, request.Name, request.Description, request.Permissions ?? [], request.ExpectedVersion,
                User.GetUserId(), ActorUsername),
            ct));

    /// <summary>Archives a role: nobody new can be given it; those who hold it keep its permissions.</summary>
    [HttpPost("roles/{id:guid}/archive")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveRole(Guid id, [FromBody] RoleVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetRoleArchivedCommand(id, true, request.ExpectedVersion, User.GetUserId(), ActorUsername), ct));

    [HttpPost("roles/{id:guid}/restore")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreRole(Guid id, [FromBody] RoleVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetRoleArchivedCommand(id, false, request.ExpectedVersion, User.GetUserId(), ActorUsername), ct));

    /// <summary>Deletes a role nobody holds; one that anyone holds is a 409 (archive it instead).</summary>
    [HttpDelete("roles/{id:guid}")]
    [RequirePermission(Permission.ManageRoles)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteRoleCommand(id, User.GetUserId(), ActorUsername), ct);
        return NoContent();
    }

    private string ActorUsername => User.Identity?.Name ?? string.Empty;
}
