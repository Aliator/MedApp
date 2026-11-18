using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;
using MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;
using MedApp.Application.Calendar.Meetings.Queries.GetMeetingById;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Meetings: time an admin arranges for several members of staff, busy on each attendee's calendar. Admins add, change
/// and cancel them; every member of staff can see them. Each must sit inside one working day, not on a past day (409),
/// and clash with nothing on any attendee's calendar (409, naming who and what).
/// </summary>
[ApiController]
[Route("api/calendar/meetings")]
[Tags("Calendar Meetings")]
public sealed class MeetingsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetMeeting(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeetingByIdQuery(id, User.ToCalendarActor()), ct));

    [HttpPost]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ArrangeMeetings)]
    public async Task<IActionResult> CreateMeeting([FromBody] MeetingRequest request, CancellationToken ct)
    {
        var meeting = await mediator.Send(new CreateMeetingCommand(
            request.Title,
            request.Notes,
            request.StartUtc,
            request.EndUtc,
            request.AttendeeUserIds ?? [],
            User.ToCalendarActor(),
            request.IsMdt,
            request.SpecialtyId), ct);

        return Created($"/api/calendar/meetings/{meeting.Id}", meeting);
    }

    /// <summary>
    /// Changes a meeting's title, notes, time and attendees. The request carries the meeting's <c>lastUpdated</c> as the
    /// caller last read it (<c>expectedLastUpdated</c>): a 409 if it has changed since.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ArrangeMeetings)]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] MeetingRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateMeetingCommand(
            id,
            request.Title,
            request.Notes,
            request.StartUtc,
            request.EndUtc,
            request.AttendeeUserIds ?? [],
            request.ExpectedLastUpdated,
            User.ToCalendarActor(),
            request.IsMdt,
            request.SpecialtyId), ct));

    /// <summary>
    /// The signed-in member of staff accepts or declines a meeting they're going to (<c>reply</c>: "Accepted" or
    /// "Declined"), before it ends; they can change their reply. Only for themselves: someone not going is a 403, and
    /// a cancelled meeting a 404. It carries the start and end the person saw (<c>expectedStartUtc</c>,
    /// <c>expectedEndUtc</c>): a 409 if the meeting has moved since. A decline frees the time on their calendar and
    /// tells the organiser.
    /// </summary>
    [HttpPut("{id:guid}/reply")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> ReplyToMeeting(Guid id, [FromBody] MeetingReplyRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new ReplyToMeetingCommand(id, request.Reply, request.ExpectedStartUtc, request.ExpectedEndUtc, User.ToCalendarActor()), ct));

    /// <summary>Cancels (deletes) a meeting; its attendees are told.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ArrangeMeetings)]
    public async Task<IActionResult> CancelMeeting(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelMeetingCommand(id, User.ToCalendarActor()), ct);
        return NoContent();
    }
}
