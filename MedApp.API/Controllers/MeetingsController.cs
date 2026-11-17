using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;
using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
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
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class MeetingsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeeting(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeetingByIdQuery(id, User.ToCalendarActor()), ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMeeting([FromBody] MeetingRequest request, CancellationToken ct)
    {
        var meeting = await mediator.Send(new CreateMeetingCommand(
            request.Title,
            request.Notes,
            request.StartUtc,
            request.EndUtc,
            request.AttendeeUserIds ?? [],
            User.ToCalendarActor()), ct);

        return Created($"/api/calendar/meetings/{meeting.Id}", meeting);
    }

    /// <summary>
    /// Changes a meeting's title, notes, time and attendees. The request carries the meeting's <c>lastUpdated</c> as the
    /// caller last read it (<c>expectedLastUpdated</c>): a 409 if it has changed since.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(MeetingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] MeetingRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateMeetingCommand(
            id,
            request.Title,
            request.Notes,
            request.StartUtc,
            request.EndUtc,
            request.AttendeeUserIds ?? [],
            request.ExpectedLastUpdated,
            User.ToCalendarActor()), ct));

    /// <summary>Cancels (deletes) a meeting; its attendees are told.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelMeeting(Guid id, CancellationToken ct)
    {
        await mediator.Send(new CancelMeetingCommand(id, User.ToCalendarActor()), ct);
        return NoContent();
    }
}
