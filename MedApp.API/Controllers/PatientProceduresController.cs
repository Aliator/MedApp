using System.Security.Claims;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProcedures;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProceduresForPatient;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetAllPatientProceduresForUser;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/procedures/patient-procedures")]
public sealed class PatientProceduresController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status201Created)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> CreatePatientProcedure([FromBody] CreatePatientProcedureRequest request)
    {
        var command = new CreatePatientProcedureCommand(
            request.PatientId,
            request.Title,
            request.Notes,
            request.DueDateUtc,
            request.Priority,
            request.StageDefinitionIdsInOrder);

        var procedure = await mediator.Send(command);

        return Created($"/api/procedures/patient-procedures/{procedure.Id}", procedure);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> UpdatePatientProcedure(Guid id, [FromBody] UpdatePatientProcedureRequest request)
    {
        var command = new UpdatePatientProcedureCommand(
            id,
            request.Title,
            request.Notes,
            request.DueDateUtc,
            request.Priority,
            request.Status,
            request.StageDefinitionIdsInOrder);

        var updatedProcedure = await mediator.Send(command);

        return Ok(updatedProcedure);
    }

    [HttpPost("{patientProcedureId:guid}/assign/{userId:guid}")]
    [ProducesResponseType(typeof(List<PatientProcedureAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> AssignPatientProcedure(Guid patientProcedureId, Guid userId, CancellationToken ct)
    {
        var assignedByUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var result = await mediator.Send(
            new AssignPatientProcedureCommand(patientProcedureId, userId, assignedByUserId),
            ct);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> DeletePatientProcedure(Guid id)
    {
        var command = new DeletePatientProcedureCommand(id);

        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> GetAllPatientProcedures()
    {
        var query = new GetAllPatientProceduresQuery();
        var procedures = await mediator.Send(query);

        return Ok(procedures);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> GetPatientProcedureById(Guid id)
    {
        var query = new GetPatientProcedureByIdQuery(id);
        var procedure = await mediator.Send(query);

        return Ok(procedure);
    }

    [HttpGet("user/self")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureResponse>), StatusCodes.Status200OK)]
    [Authorize]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> GetAllPatientProceduresForCurrentUser(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var query = new GetAllPatientProceduresForUserQuery(userId);
        var procedures = await mediator.Send(query, ct);

        return Ok(procedures);
    }

    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> GetAllPatientProceduresForUser(Guid userId, CancellationToken ct)
    {
        var query = new GetAllPatientProceduresForUserQuery(userId);
        var procedures = await mediator.Send(query, ct);

        return Ok(procedures);
    }

    [HttpGet("patient/{patientId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Procedures")]
    public async Task<IActionResult> GetAllPatientProceduresForPatient(Guid patientId, CancellationToken ct)
    {
        var query = new GetAllPatientProceduresForPatientQuery(patientId);
        var procedures = await mediator.Send(query, ct);

        return Ok(procedures);
    }
}