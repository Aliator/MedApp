using MedApp.API.Common.Authentication;
using MedApp.Application.Clinics.Commands.CancelClinicSession;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Commands.CreateClinicSession;
using MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;
using MedApp.Application.Clinics.Commands.UpdateClinicPattern;
using MedApp.Application.Clinics.Commands.UpdateClinicSession;
using MedApp.Application.Clinics.Queries.GetClinicPatterns;
using MedApp.Application.Clinics.Queries.GetClinicSessionById;
using MedApp.Application.Clinics.Queries.SearchClinicSessions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Clinics.Requests;
using MedApp.Contracts.Clinics.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Clinic sessions and patterns (centre plan decisions 123 to 127): a clinician's time in a room for a team, cut
/// into slots that appointments are booked into, and the patterns that make them when someone asks. Anyone signed in
/// reads sessions; holders of ManageClinics add, change and cancel them and keep the patterns.
/// </summary>
[ApiController]
[Route("api/clinics")]
[Tags("Clinics")]
public sealed class ClinicsController(IMediator mediator) : ControllerBase
{
    /// <summary>Sessions starting from one UK date to another (at most 62 days), by start. Names no patient.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicSessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> SearchSessions([FromQuery] SearchClinicSessionsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchClinicSessionsQuery(
            request.From,
            request.To,
            request.StaffUserId,
            request.SiteId,
            request.RoomId,
            request.TeamId,
            request.IncludeCancelled,
            request.WithFreeSlots,
            User.ToCalendarActor()), ct));

    /// <summary>
    /// A session with its slots and who is booked into each, and once it's cancelled the patients to tell (recorded as a
    /// read of those patients; never cached).
    /// </summary>
    [HttpGet("sessions/{id:guid}")]
    [ProducesResponseType(typeof(ClinicSessionDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var detail = await mediator.Send(new GetClinicSessionByIdQuery(id, User.ToCalendarActor()), ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(detail);
    }

    /// <summary>
    /// Adds a session: in the future, inside the clinician's working day and the room's site's opening hours, clear of
    /// everything on their calendar and in the room (409 naming the clash), cut into whole slots.
    /// </summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(ClinicSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> CreateSession([FromBody] ClinicSessionRequest request, CancellationToken ct)
    {
        var session = await mediator.Send(new CreateClinicSessionCommand(
            request.StaffUserId,
            request.RoomId,
            request.TeamId,
            request.StartUtc,
            request.EndUtc,
            request.SlotMinutes,
            User.ToCalendarActor()), ct);

        return Created($"/api/clinics/sessions/{session.Id}", session);
    }

    /// <summary>
    /// Changes a session's room, team, time or slot length, from the version read (<c>expectedLastUpdated</c>, a 409
    /// if it changed since). With appointments booked into it, only the room (if free) and the team can change.
    /// </summary>
    [HttpPut("sessions/{id:guid}")]
    [ProducesResponseType(typeof(ClinicSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] UpdateClinicSessionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateClinicSessionCommand(
            id,
            request.RoomId,
            request.TeamId,
            request.StartUtc,
            request.EndUtc,
            request.SlotMinutes,
            request.ExpectedLastUpdated,
            User.ToCalendarActor()), ct));

    /// <summary>
    /// Cancels a session and every appointment booked into it, with a reason, and lists the patients to tell. Its
    /// clinician is told when someone else cancels it.
    /// </summary>
    [HttpPost("sessions/{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelClinicSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> CancelSession(Guid id, [FromBody] CancelClinicSessionRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CancelClinicSessionCommand(id, request.Reason, request.ExpectedLastUpdated, User.ToCalendarActor(), request.TellPatients), ct);

        // It names patients: never kept by a browser or proxy.
        Response.Headers.CacheControl = "no-store";
        return Ok(result);
    }

    /// <summary>Every clinic pattern.</summary>
    [HttpGet("patterns")]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicPatternResponse>), StatusCodes.Status200OK)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> GetPatterns(CancellationToken ct) =>
        Ok(await mediator.Send(new GetClinicPatternsQuery(), ct));

    /// <summary>Adds a clinic pattern. It makes no sessions until someone asks (Create sessions).</summary>
    [HttpPost("patterns")]
    [ProducesResponseType(typeof(ClinicPatternResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> CreatePattern([FromBody] ClinicPatternRequest request, CancellationToken ct)
    {
        var pattern = await mediator.Send(new CreateClinicPatternCommand(
            request.StaffUserId,
            request.RoomId,
            request.TeamId,
            request.DayOfWeek,
            request.Starts,
            request.Ends,
            request.SlotMinutes,
            request.EveryWeeks,
            request.FromDate,
            request.UntilDate,
            User.ToCalendarActor()), ct);

        return Created($"/api/clinics/patterns/{pattern.Id}", pattern);
    }

    /// <summary>Changes a clinic pattern (<c>expectedLastUpdated</c> required); sessions already made stay as they are.</summary>
    [HttpPut("patterns/{id:guid}")]
    [ProducesResponseType(typeof(ClinicPatternResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> UpdatePattern(Guid id, [FromBody] ClinicPatternRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateClinicPatternCommand(
            id,
            request.StaffUserId,
            request.RoomId,
            request.TeamId,
            request.DayOfWeek,
            request.Starts,
            request.Ends,
            request.SlotMinutes,
            request.EveryWeeks,
            request.FromDate,
            request.UntilDate,
            request.ExpectedLastUpdated,
            User.ToCalendarActor()), ct));

    /// <summary>
    /// Create sessions: makes a pattern's sessions for the next 1 to 13 weeks, skipping dates already made, closed days,
    /// days the clinician doesn't work and clashes, and says what it made and skipped.
    /// </summary>
    [HttpPost("patterns/{id:guid}/create-sessions")]
    [ProducesResponseType(typeof(CreateSessionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ManageClinics)]
    public async Task<IActionResult> CreateSessions(Guid id, [FromBody] CreateSessionsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new CreateSessionsFromPatternCommand(id, request.Weeks, User.ToCalendarActor()), ct));
}
