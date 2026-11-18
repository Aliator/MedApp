using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Application.Teams.Commands.DeleteTeam;
using MedApp.Application.Teams.Commands.SetTeamArchived;
using MedApp.Application.Teams.Commands.UpdateTeam;
using MedApp.Application.Teams.Queries.GetTeams;
using MedApp.Application.Teams.Queries.GetTeamById;
using MedApp.Contracts.Teams.Requests;
using MedApp.Contracts.Teams.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The teams the centre offers (organisation-wide: holders of ManageSetup manage them, anyone with a role reads them). Every pathway template
/// belongs to one, a pathway keeps its template's, and staff belong to any number (<c>api/auth/users/{username}/teams</c>).
/// One that's in use can't be deleted, only archived (it can't be chosen again) and restored. Every change carries the
/// version the caller saw (<c>expectedVersion</c>): 409 if another admin has changed it since.
/// </summary>
[ApiController]
[Route("api/teams")]
[Tags("Teams")]
public sealed class TeamsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<TeamResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeams([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetTeamsQuery(includeArchived), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeam(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetTeamByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var team = await mediator.Send(new CreateTeamCommand(
            request.Name, request.TargetDaysUrgent, request.TargetDaysHigh, request.TargetDaysMedium, request.TargetDaysLow), ct);
        return Created($"/api/teams/{team.Id}", team);
    }

    /// <summary>Renames a team; what has it shows the new name.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTeam(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateTeamCommand(
            id, request.Name, request.ExpectedVersion,
            request.TargetDaysUrgent, request.TargetDaysHigh, request.TargetDaysMedium, request.TargetDaysLow), ct));

    /// <summary>Archives a team: it can't be chosen for templates or given to staff; what has it keeps it.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveTeam(Guid id, [FromBody] TeamVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetTeamArchivedCommand(id, true, request.ExpectedVersion), ct));

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(TeamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreTeam(Guid id, [FromBody] TeamVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetTeamArchivedCommand(id, false, request.ExpectedVersion), ct));

    /// <summary>Deletes a team nothing has; one that a template, pathway or member of staff has is a 409 (archive it).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteTeam(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteTeamCommand(id), ct);
        return NoContent();
    }
}
