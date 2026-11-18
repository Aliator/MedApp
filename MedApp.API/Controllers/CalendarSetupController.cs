using MedApp.API.Common.Authentication;
using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;
using MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;
using MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;
using MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;
using MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;
using MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;
using MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;
using MedApp.Contracts.Calendar.Requests;
using MedApp.Contracts.Calendar.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// What calendars are set up with: activity types (the preset list for blocked time, organisation-wide: admins manage
/// them, staff read them), closed days (bank holidays and other days the clinic is closed, organisation-wide: admins
/// manage them, staff read them), and each member of staff's own day templates and week templates (their owner or an admin
/// changes them, 403 otherwise; anyone reads them). One that has been used can't be deleted, only archived (it can't be
/// chosen again) and restored. Editing a template applies from today; past days keep what they had.
/// </summary>
[ApiController]
[Route("api/calendar")]
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class CalendarSetupController(IMediator mediator) : ControllerBase
{
    private const string ActivityTypesTag = "Calendar Activity Types";
    private const string DayTemplatesTag = "Calendar Day Templates";
    private const string WeekTemplatesTag = "Calendar Week Templates";
    private const string ClosedDaysTag = "Calendar Closed Days";

    [HttpGet("activity-types")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityTypeResponse>), StatusCodes.Status200OK)]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> GetActivityTypes([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetActivityTypesQuery(includeArchived), ct));

    [HttpGet("activity-types/{id:guid}")]
    [ProducesResponseType(typeof(ActivityTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> GetActivityType(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetActivityTypeByIdQuery(id), ct));

    [HttpPost("activity-types")]
    [ProducesResponseType(typeof(ActivityTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> CreateActivityType([FromBody] CreateActivityTypeRequest request, CancellationToken ct)
    {
        var activityType = await mediator.Send(new CreateActivityTypeCommand(request.Name, request.Colour), ct);
        return Created($"/api/calendar/activity-types/{activityType.Id}", activityType);
    }

    [HttpPatch("activity-types/{id:guid}")]
    [ProducesResponseType(typeof(ActivityTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> UpdateActivityType(Guid id, [FromBody] UpdateActivityTypeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateActivityTypeCommand(id, request.Name, request.Colour), ct));

    [HttpPost("activity-types/{id:guid}/archive")]
    [ProducesResponseType(typeof(ActivityTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> ArchiveActivityType(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetActivityTypeArchivedCommand(id, true), ct));

    [HttpPost("activity-types/{id:guid}/restore")]
    [ProducesResponseType(typeof(ActivityTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> RestoreActivityType(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetActivityTypeArchivedCommand(id, false), ct));

    /// <summary>Deletes an activity type no block has used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("activity-types/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ActivityTypesTag)]
    public async Task<IActionResult> DeleteActivityType(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteActivityTypeCommand(id), ct);
        return NoContent();
    }

    /// <summary>A member of staff's day templates (anyone's can be read).</summary>
    [HttpGet("staff/{userId:guid}/day-templates")]
    [ProducesResponseType(typeof(IReadOnlyList<DayTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> GetDayTemplates(Guid userId, [FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDayTemplatesQuery(userId, User.ToCalendarActor(), includeArchived), ct));

    [HttpGet("day-templates/{id:guid}")]
    [ProducesResponseType(typeof(DayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> GetDayTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDayTemplateByIdQuery(id, User.ToCalendarActor()), ct));

    /// <summary>Adds a day template to a member of staff's own: their own, or anyone's for an admin.</summary>
    [HttpPost("staff/{userId:guid}/day-templates")]
    [ProducesResponseType(typeof(DayTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> CreateDayTemplate(Guid userId, [FromBody] DayTemplateRequest request, CancellationToken ct)
    {
        var template = await mediator.Send(
            new CreateDayTemplateCommand(userId, request.Name, Blocks(request), User.ToCalendarActor()), ct);
        return Created($"/api/calendar/day-templates/{template.Id}", template);
    }

    /// <summary>Renames a day template and replaces its blocks, from today.</summary>
    [HttpPut("day-templates/{id:guid}")]
    [ProducesResponseType(typeof(DayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> UpdateDayTemplate(Guid id, [FromBody] DayTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateDayTemplateCommand(id, request.Name, Blocks(request), User.ToCalendarActor()), ct));

    [HttpPost("day-templates/{id:guid}/archive")]
    [ProducesResponseType(typeof(DayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> ArchiveDayTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetDayTemplateArchivedCommand(id, true, User.ToCalendarActor()), ct));

    [HttpPost("day-templates/{id:guid}/restore")]
    [ProducesResponseType(typeof(DayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> RestoreDayTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetDayTemplateArchivedCommand(id, false, User.ToCalendarActor()), ct));

    /// <summary>Deletes a day template no week template or day has used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("day-templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(DayTemplatesTag)]
    public async Task<IActionResult> DeleteDayTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDayTemplateCommand(id, User.ToCalendarActor()), ct);
        return NoContent();
    }

    /// <summary>A member of staff's week templates (anyone's can be read).</summary>
    [HttpGet("staff/{userId:guid}/week-templates")]
    [ProducesResponseType(typeof(IReadOnlyList<WeekTemplateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> GetWeekTemplates(Guid userId, [FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetWeekTemplatesQuery(userId, User.ToCalendarActor(), includeArchived), ct));

    [HttpGet("week-templates/{id:guid}")]
    [ProducesResponseType(typeof(WeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> GetWeekTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetWeekTemplateByIdQuery(id, User.ToCalendarActor()), ct));

    /// <summary>Adds a week template to a member of staff's own, using only their day templates.</summary>
    [HttpPost("staff/{userId:guid}/week-templates")]
    [ProducesResponseType(typeof(WeekTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> CreateWeekTemplate(Guid userId, [FromBody] WeekTemplateRequest request, CancellationToken ct)
    {
        var template = await mediator.Send(new CreateWeekTemplateCommand(
            userId,
            request.Name,
            request.Monday,
            request.Tuesday,
            request.Wednesday,
            request.Thursday,
            request.Friday,
            User.ToCalendarActor()), ct);

        return Created($"/api/calendar/week-templates/{template.Id}", template);
    }

    /// <summary>Renames a week template and replaces its days, from today.</summary>
    [HttpPut("week-templates/{id:guid}")]
    [ProducesResponseType(typeof(WeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> UpdateWeekTemplate(Guid id, [FromBody] WeekTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateWeekTemplateCommand(
            id,
            request.Name,
            request.Monday,
            request.Tuesday,
            request.Wednesday,
            request.Thursday,
            request.Friday,
            User.ToCalendarActor()), ct));

    [HttpPost("week-templates/{id:guid}/archive")]
    [ProducesResponseType(typeof(WeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> ArchiveWeekTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetWeekTemplateArchivedCommand(id, true, User.ToCalendarActor()), ct));

    [HttpPost("week-templates/{id:guid}/restore")]
    [ProducesResponseType(typeof(WeekTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> RestoreWeekTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetWeekTemplateArchivedCommand(id, false, User.ToCalendarActor()), ct));

    /// <summary>Deletes a week template that has never been anyone's default; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("week-templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(WeekTemplatesTag)]
    public async Task<IActionResult> DeleteWeekTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteWeekTemplateCommand(id, User.ToCalendarActor()), ct);
        return NoContent();
    }

    /// <summary>The closed days in a range of UK dates (either end may be left open), by date.</summary>
    [HttpGet("closed-days")]
    [ProducesResponseType(typeof(IReadOnlyList<ClosedDayResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Tags(ClosedDaysTag)]
    public async Task<IActionResult> GetClosedDays([FromQuery] GetClosedDaysRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetClosedDaysQuery(request.From, request.To), ct));

    /// <summary>Closes the clinic on a day, today or later. What's already on it stays, flagged.</summary>
    [HttpPost("closed-days")]
    [ProducesResponseType(typeof(ClosedDayResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ClosedDaysTag)]
    public async Task<IActionResult> CreateClosedDay([FromBody] CreateClosedDayRequest request, CancellationToken ct)
    {
        var day = await mediator.Send(new CreateClosedDayCommand(request.Date, request.Name, User.ToCalendarActor()), ct);
        return Created($"/api/calendar/closed-days?from={day.Date:yyyy-MM-dd}&to={day.Date:yyyy-MM-dd}", day);
    }

    /// <summary>Adds a year's England and Wales bank holidays as closed days; dates already closed, and past ones, are skipped.</summary>
    [HttpPost("closed-days/bank-holidays")]
    [ProducesResponseType(typeof(AddBankHolidaysResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ClosedDaysTag)]
    public async Task<IActionResult> AddBankHolidays([FromBody] AddBankHolidaysRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AddBankHolidaysCommand(request.Year, User.ToCalendarActor()), ct));

    /// <summary>Opens the clinic again on a closed day, today or later.</summary>
    [HttpDelete("closed-days/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags(ClosedDaysTag)]
    public async Task<IActionResult> DeleteClosedDay(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteClosedDayCommand(id), ct);
        return NoContent();
    }

    private static List<DayTemplateBlockValue> Blocks(DayTemplateRequest request) =>
        (request.Blocks ?? [])
            .Select(b => new DayTemplateBlockValue(b.ActivityTypeId, b.StartTime, b.EndTime, b.Note))
            .ToList();
}
