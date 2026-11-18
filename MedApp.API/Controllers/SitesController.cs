using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Sites.Commands.CreateRoom;
using MedApp.Application.Sites.Commands.CreateSite;
using MedApp.Application.Sites.Commands.DeleteRoom;
using MedApp.Application.Sites.Commands.DeleteSite;
using MedApp.Application.Sites.Commands.SetRoomArchived;
using MedApp.Application.Sites.Commands.SetSiteArchived;
using MedApp.Application.Sites.Commands.SetSiteHours;
using MedApp.Application.Sites.Commands.UpdateRoom;
using MedApp.Application.Sites.Commands.UpdateSite;
using MedApp.Application.Sites.Queries.GetRooms;
using MedApp.Application.Sites.Queries.GetSiteById;
using MedApp.Application.Sites.Queries.GetSiteHours;
using MedApp.Application.Sites.Queries.GetSites;
using MedApp.Contracts.Sites.Requests;
using MedApp.Contracts.Sites.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Sites (the places the centre works from) and their rooms: organisation-wide setup, which admins manage and staff
/// read (`ManageSetup` to change, `UseApp` to read). One that has been used is archived, not deleted (it can't be chosen again, and what names it keeps it), and
/// restored. There's always at least one site that isn't archived; every database starts with "Main site".
/// </summary>
[ApiController]
[Route("api")]
[RequirePermission(Permission.UseApp)]
public sealed class SitesController(IMediator mediator) : ControllerBase
{
    private const string SitesTag = "Sites";
    private const string RoomsTag = "Rooms";

    /// <summary>Every site by name, each with its rooms; archived sites and rooms only with <c>includeArchived</c>.</summary>
    [HttpGet("sites")]
    [ProducesResponseType(typeof(IReadOnlyList<SiteResponse>), StatusCodes.Status200OK)]
    [Tags(SitesTag)]
    public async Task<IActionResult> GetSites([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSitesQuery(includeArchived), ct));

    /// <summary>One site with all its rooms, archived ones too.</summary>
    [HttpGet("sites/{id:guid}")]
    [ProducesResponseType(typeof(SiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(SitesTag)]
    public async Task<IActionResult> GetSite(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSiteByIdQuery(id), ct));

    [HttpPost("sites")]
    [ProducesResponseType(typeof(SiteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> CreateSite([FromBody] SiteRequest request, CancellationToken ct)
    {
        var site = await mediator.Send(
            new CreateSiteCommand(request.Name, request.AddressLines, request.Postcode, request.Phone), ct);
        return Created($"/api/sites/{site.Id}", site);
    }

    /// <summary>Replaces a site's details: an address, postcode or phone left out is removed.</summary>
    [HttpPut("sites/{id:guid}")]
    [ProducesResponseType(typeof(SiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> UpdateSite(Guid id, [FromBody] SiteRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new UpdateSiteCommand(id, request.Name, request.AddressLines, request.Postcode, request.Phone), ct));

    /// <summary>Archives a site: refused (409) while it has rooms that aren't archived, or if it's the last open site.</summary>
    [HttpPost("sites/{id:guid}/archive")]
    [ProducesResponseType(typeof(SiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> ArchiveSite(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSiteArchivedCommand(id, true), ct));

    [HttpPost("sites/{id:guid}/restore")]
    [ProducesResponseType(typeof(SiteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> RestoreSite(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSiteArchivedCommand(id, false), ct));

    /// <summary>Deletes a site with no rooms and no closed days; a used one, or the last open site, is a 409.</summary>
    [HttpDelete("sites/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> DeleteSite(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteSiteCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Rooms by site, then name: one site's (<c>siteId</c>) or every site's. Archived rooms, and archived sites' rooms,
    /// only with <c>includeArchived</c>.
    /// </summary>
    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> GetRooms([FromQuery] Guid? siteId, [FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetRoomsQuery(siteId, includeArchived), ct));

    /// <summary>Adds a room to a site that isn't archived.</summary>
    [HttpPost("rooms")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var room = await mediator.Send(new CreateRoomCommand(request.SiteId, request.Name), ct);
        return Created($"/api/rooms?siteId={room.SiteId}", room);
    }

    /// <summary>Renames a room.</summary>
    [HttpPut("rooms/{id:guid}")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> UpdateRoom(Guid id, [FromBody] UpdateRoomRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateRoomCommand(id, request.Name), ct));

    [HttpPost("rooms/{id:guid}/archive")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> ArchiveRoom(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetRoomArchivedCommand(id, true), ct));

    /// <summary>Restores a room: refused (409) while its site is archived.</summary>
    [HttpPost("rooms/{id:guid}/restore")]
    [ProducesResponseType(typeof(RoomResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> RestoreRoom(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetRoomArchivedCommand(id, false), ct));

    /// <summary>Deletes a room nothing has used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("rooms/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(RoomsTag)]
    public async Task<IActionResult> DeleteRoom(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteRoomCommand(id), ct);
        return NoContent();
    }

    /// <summary>A site's opening hours today: its own, or the default (Monday to Friday, 08:00 to 18:00) when it has none.</summary>
    [HttpGet("sites/{id:guid}/hours")]
    [ProducesResponseType(typeof(SiteHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(SitesTag)]
    public async Task<IActionResult> GetSiteHours(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSiteHoursQuery(id), ct));

    /// <summary>
    /// Sets a site's opening hours from today (past days keep theirs): one entry per day it's open (an evening clinic is
    /// a Tuesday until 20:00); none puts it back on the default. Refused (409) for an archived site, or while anyone's
    /// working hours there would fall outside them.
    /// </summary>
    [HttpPut("sites/{id:guid}/hours")]
    [ProducesResponseType(typeof(SiteHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageSetup)]
    [Tags(SitesTag)]
    public async Task<IActionResult> SetSiteHours(Guid id, [FromBody] SiteHoursRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSiteHoursCommand(
            id, request.Days?.Select(d => new SiteHoursDay(d.DayOfWeek, d.Opens, d.Closes)).ToList()), ct));
}
