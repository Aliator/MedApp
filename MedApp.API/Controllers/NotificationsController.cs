using MedApp.API.Common.Authentication;
using MedApp.Application.Notifications.Commands.MarkNotificationsRead;
using MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;
using MedApp.Application.Notifications.Queries.GetMyNotificationPreferences;
using MedApp.Application.Notifications.Queries.GetMyNotifications;
using MedApp.Contracts.Notifications.Requests;
using MedApp.Contracts.Notifications.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>The signed-in user's notifications. New ones also arrive live, through the notifications hub.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AuthPolicies.Staff)]
[Tags("Notifications")]
public sealed class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NotificationsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] int? take, CancellationToken ct) =>
        Ok(await mediator.Send(
            new GetMyNotificationsQuery(User.GetUserId(), take ?? GetMyNotificationsQuery.DefaultTake), ct));

    /// <summary>Marks every unread notification read.</summary>
    [HttpPost("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationsReadCommand(User.GetUserId(), null), ct);
        return NoContent();
    }

    /// <summary>Marks one notification read (nothing happens if it isn't yours or is read already).</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationsReadCommand(User.GetUserId(), id), ct);
        return NoContent();
    }

    /// <summary>Which notifications you get (everything, until you choose).</summary>
    [HttpGet("preferences")]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPreferences(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyNotificationPreferencesQuery(User.GetUserId()), ct));

    /// <summary>Chooses which notifications you get.</summary>
    [HttpPut("preferences")]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdateNotificationPreferencesRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateMyNotificationPreferencesCommand(User.GetUserId(), request.ProcedureAssigned), ct));
}
