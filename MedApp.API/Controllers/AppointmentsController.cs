using MedApp.API.Common.Authentication;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;
using MedApp.Application.Appointments.Queries.GetAppointmentById;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Appointments: a patient seeing a member of staff, optionally for a procedure and stage. Any member of staff can
/// request, book, move, cancel and record the outcome of any appointment, for anyone. Every change is recorded in its
/// history, which the patient's timeline shows.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Tags("Appointments")]
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class AppointmentsController(IMediator mediator) : ControllerBase
{
    /// <summary>Books an appointment (with a member of staff, a start and an end) or adds one to Requires Appointment (with none of them).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await mediator.Send(new CreateAppointmentCommand(
            request.PatientId,
            request.ProcedureId,
            request.StageId,
            request.StaffUserId,
            request.StartUtc,
            request.EndUtc,
            request.Notes,
            User.ToProcedureActor()), ct);

        return Created($"/api/appointments/{appointment.Id}", appointment);
    }

    /// <summary>
    /// Records an appointment that has already happened (a walk-in, or one booked outside MedApp) with its outcome:
    /// started within the last 30 days, on a working day. A stage's visit in Requires Appointment becomes this one.
    /// </summary>
    [HttpPost("past")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordPastAppointment([FromBody] RecordPastAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await mediator.Send(new RecordPastAppointmentCommand(
            request.PatientId,
            request.ProcedureId,
            request.StageId,
            request.StaffUserId,
            request.StartUtc,
            request.EndUtc,
            request.Outcome,
            request.Notes,
            User.ToProcedureActor()), ct);

        return Created($"/api/appointments/{appointment.Id}", appointment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAppointmentByIdQuery(id), ct));

    /// <summary>One page of appointments. By default sorted by start, soonest first, with Requested ones last.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<AppointmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAppointments([FromQuery] SearchAppointmentsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchAppointmentsQuery(
            request.Page,
            request.PageSize,
            request.Sort,
            request.Ascending,
            request.PatientId,
            request.ProcedureId,
            request.StageId,
            request.StaffUserId,
            request.Statuses,
            request.From,
            request.To,
            request.Search), ct));

    /// <summary>Books a Requested appointment, or moves a Booked one to another time or member of staff.</summary>
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookAppointment(Guid id, [FromBody] BookAppointmentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new BookAppointmentCommand(
            id,
            request.StaffUserId,
            request.StartUtc,
            request.EndUtc,
            request.ExpectedStatus,
            User.ToProcedureActor()), ct));

    /// <summary>Records Attended or DidNotAttend, once the appointment has started (or corrects it).</summary>
    [HttpPost("{id:guid}/outcome")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordOutcome(Guid id, [FromBody] RecordAppointmentOutcomeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordAppointmentOutcomeCommand(id, request.Outcome, User.ToProcedureActor()), ct));

    /// <summary>
    /// Withdraws an appointment recorded after the event as entered in error (it didn't happen, or it's on the wrong
    /// patient), with a reason: by whoever recorded it, or an admin, within 30 days of it.
    /// </summary>
    [HttpPost("{id:guid}/entered-in-error")]
    [ProducesResponseType(typeof(EnteredInErrorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] MarkEnteredInErrorRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkAppointmentEnteredInErrorCommand(id, request.Reason, User.ToProcedureActor(), User.IsInRole("Admin")), ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAppointment(Guid id, [FromBody] CancelAppointmentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new CancelAppointmentCommand(id, request.Reason, User.ToProcedureActor()), ct));

    [HttpPut("{id:guid}/notes")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] UpdateAppointmentNotesRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateAppointmentNotesCommand(id, request.Notes, User.ToProcedureActor()), ct));
}
