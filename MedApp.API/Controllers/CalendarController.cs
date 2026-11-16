using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Members of staff's calendars: an 08:00 to 18:00 working day, UK time, Monday to Friday, with their appointments and
/// blocked time. Any member of staff can see anyone's calendar; its blocks, a day's template and the default week can
/// be changed by its owner or an admin (403 otherwise), never on a past day (409).
/// </summary>
[ApiController]
[Route("api/calendar")]
[Tags("Calendar")]
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class CalendarController(IMediator mediator) : ControllerBase
{
    /// <summary>One person's calendar (yours by default) from one UK date to another, at most 31 days.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CalendarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendar([FromQuery] GetCalendarRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCalendarQuery(request.StaffUserId, request.From, request.To, User.ToCalendarActor()), ct));

    /// <summary>Everyone whose calendar can be shown (anyone with a role), with their default week template.</summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaff(CancellationToken ct) =>
        Ok(await mediator.Send(new GetCalendarStaffQuery(), ct));

    /// <summary>Blocks off time for an activity, inside the working day and clash-free.</summary>
    [HttpPost("blocks")]
    [ProducesResponseType(typeof(CalendarBlockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBlock([FromBody] CreateCalendarBlockRequest request, CancellationToken ct)
    {
        var block = await mediator.Send(new CreateCalendarBlockCommand(
            request.StaffUserId,
            request.ActivityTypeId,
            request.StartUtc,
            request.EndUtc,
            request.Note,
            User.ToCalendarActor()), ct);

        return Created($"/api/calendar/blocks/{block.BlockId}", block);
    }

    [HttpPut("blocks/{id:guid}")]
    [ProducesResponseType(typeof(CalendarBlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBlock(Guid id, [FromBody] UpdateCalendarBlockRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateCalendarBlockCommand(
            id,
            request.ActivityTypeId,
            request.StartUtc,
            request.EndUtc,
            request.Note,
            User.ToCalendarActor()), ct));

    [HttpDelete("blocks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBlock(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteCalendarBlockCommand(id, User.ToCalendarActor()), ct);
        return NoContent();
    }

    /// <summary>
    /// Changes one day's template block: it's left off that day, and a block of its own with these values (on the same
    /// day) takes its place. The template is unchanged.
    /// </summary>
    [HttpPut("staff/{userId:guid}/days/{date}/template-blocks/{templateBlockId:guid}")]
    [ProducesResponseType(typeof(CalendarBlockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeTemplateBlockForDay(
        Guid userId,
        DateOnly date,
        Guid templateBlockId,
        [FromBody] UpdateCalendarBlockRequest request,
        CancellationToken ct) =>
        Ok(await mediator.Send(new ChangeTemplateBlockForDayCommand(
            userId,
            date,
            templateBlockId,
            request.ActivityTypeId,
            request.StartUtc,
            request.EndUtc,
            request.Note,
            User.ToCalendarActor()), ct));

    /// <summary>Removes one day's template block. The template is unchanged.</summary>
    [HttpDelete("staff/{userId:guid}/days/{date}/template-blocks/{templateBlockId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveTemplateBlockForDay(Guid userId, DateOnly date, Guid templateBlockId, CancellationToken ct)
    {
        await mediator.Send(new RemoveTemplateBlockForDayCommand(userId, date, templateBlockId, User.ToCalendarActor()), ct);
        return NoContent();
    }

    /// <summary>Gives a weekday a day template of its own (or none) instead of the week template's.</summary>
    [HttpPut("staff/{userId:guid}/days/{date}/day-template")]
    [ProducesResponseType(typeof(CalendarDayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyDayTemplate(
        Guid userId,
        DateOnly date,
        [FromBody] ApplyDayTemplateRequest request,
        CancellationToken ct) =>
        Ok(await mediator.Send(new ApplyDayTemplateCommand(userId, date, request.DayTemplateId, User.ToCalendarActor()), ct));

    /// <summary>Takes a day back to its week template's day template.</summary>
    [HttpDelete("staff/{userId:guid}/days/{date}/day-template")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ClearDayTemplate(Guid userId, DateOnly date, CancellationToken ct)
    {
        await mediator.Send(new ClearDayTemplateCommand(userId, date, User.ToCalendarActor()), ct);
        return NoContent();
    }

    [HttpGet("staff/{userId:guid}/week-template")]
    [ProducesResponseType(typeof(StaffWeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeekTemplate(Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetStaffWeekTemplateQuery(userId), ct));

    /// <summary>Sets a person's default week template (or none) from today; past days keep the one they had.</summary>
    [HttpPut("staff/{userId:guid}/week-template")]
    [ProducesResponseType(typeof(StaffWeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetWeekTemplate(Guid userId, [FromBody] SetStaffWeekTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetStaffWeekTemplateCommand(userId, request.WeekTemplateId, User.ToCalendarActor()), ct));
}
