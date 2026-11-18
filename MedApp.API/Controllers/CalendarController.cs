using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.Hours.Commands.SetStaffHours;
using MedApp.Application.Calendar.Hours.Queries.GetStaffHours;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.DeleteCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Members of staff's calendars: each person's working day (their hours, or the default 08:00 to 18:00 UK time, Monday
/// to Friday), with their appointments and blocked time. Any member of staff can see anyone's calendar; its blocks, a day's template and the default week can
/// be changed by its owner or an admin (403 otherwise), never on a past day (409).
/// </summary>
[ApiController]
[Route("api/calendar")]
[Tags("Calendar")]
public sealed class CalendarController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// One person's calendar (yours by default) from one UK date to another, at most 31 days; with
    /// <c>WithoutAppointments</c>, without its appointments, recording no reads of patients.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CalendarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetCalendar([FromQuery] GetCalendarRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(
            new GetCalendarQuery(request.StaffUserId, request.From, request.To, User.ToCalendarActor(), request.WithoutAppointments), ct));

    /// <summary>
    /// The team view (admins): one UK date of several people's calendars (everyone with a staff role when none are
    /// named), and when they're all free.
    /// </summary>
    [HttpGet("team")]
    [ProducesResponseType(typeof(TeamCalendarResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ArrangeMeetings)]
    public async Task<IActionResult> GetTeamCalendar([FromQuery] GetTeamCalendarRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetTeamCalendarQuery(request.Date, request.StaffUserIds ?? [], User.ToCalendarActor()), ct));

    /// <summary>
    /// When several people are all free (admins, for arranging a meeting): each working day's free time, suggested
    /// times for a meeting of the length given, and each person's busy times. Names no patient.
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ArrangeMeetings)]
    public async Task<IActionResult> GetAvailability([FromQuery] GetAvailabilityRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAvailabilityQuery(
            request.StaffUserIds ?? [],
            request.From,
            request.To,
            request.DurationMinutes,
            request.ExceptMeetingId), ct));

    /// <summary>
    /// Everyone whose calendar can be shown (anyone with a role), with their default week template. With
    /// <paramref name="permission"/> (a permission's name, exact case), only those whose roles grant it: the people list
    /// for whoever may not manage users, such as who can be assigned a pathway (<c>WorkPathways</c>) or who can unlock
    /// a stage (<c>UnlockStages</c>). A name that isn't a permission is a 400.
    /// </summary>
    [HttpGet("staff")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetStaff([FromQuery] string? permission, CancellationToken ct) =>
        Ok(await mediator.Send(new GetCalendarStaffQuery(permission), ct));

    /// <summary>Blocks off time for an activity, inside the working day and clash-free.</summary>
    [HttpPost("blocks")]
    [ProducesResponseType(typeof(CalendarBlockResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
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
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> ClearDayTemplate(Guid userId, DateOnly date, CancellationToken ct)
    {
        await mediator.Send(new ClearDayTemplateCommand(userId, date, User.ToCalendarActor()), ct);
        return NoContent();
    }

    [HttpGet("staff/{userId:guid}/week-template")]
    [ProducesResponseType(typeof(StaffWeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetWeekTemplate(Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetStaffWeekTemplateQuery(userId), ct));

    /// <summary>Sets a person's default week template (or none) from today; past days keep the one they had.</summary>
    [HttpPut("staff/{userId:guid}/week-template")]
    [ProducesResponseType(typeof(StaffWeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> SetWeekTemplate(Guid userId, [FromBody] SetStaffWeekTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetStaffWeekTemplateCommand(userId, request.WeekTemplateId, User.ToCalendarActor()), ct));

    /// <summary>
    /// A person's working hours today: their own, or the default (Monday to Friday, 08:00 to 18:00) when they have none.
    /// Anyone signed in can read them, as their calendar shows them anyway.
    /// </summary>
    [HttpGet("staff/{userId:guid}/hours")]
    [ProducesResponseType(typeof(StaffHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> GetStaffHours(Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetStaffHoursQuery(userId, User.ToCalendarActor()), ct));

    /// <summary>
    /// Sets a person's working hours from today (past days keep theirs): one entry per day they work, each inside its
    /// site's opening hours (409 otherwise); none puts them back on the default. Holders of ManageClinics or
    /// ManageAnyCalendar only (403 otherwise). What's already on their calendar stays where it is.
    /// </summary>
    [HttpPut("staff/{userId:guid}/hours")]
    [ProducesResponseType(typeof(StaffHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UseApp)]
    public async Task<IActionResult> SetStaffHours(Guid userId, [FromBody] StaffHoursRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetStaffHoursCommand(
            userId,
            request.Days?.Select(d => new StaffHoursDay(d.DayOfWeek, d.Starts, d.Ends, d.SiteId)).ToList(),
            User.ToCalendarActor()), ct));
}
