using System.Security.Claims;
using MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.DeletePatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;
using MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasks;
using MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasksForPatient;
using MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasksForUser;
using MedApp.Application.Tasks.PatientTasks.Queries.GetPatientTaskById;
using MedApp.Contracts.Tasks.PatientTasks.Requests;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/tasks/")]
public sealed class TasksController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status201Created)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> CreatePatientTask([FromBody] CreatePatientTaskRequest request)
    {
        var command = new CreatePatientTaskCommand(
            request.PatientId,
            request.Title,
            request.Notes,
            request.DueDateUtc,
            request.Priority,
            request.StageDefinitionIdsInOrder);

        var task = await mediator.Send(command);

        return Created($"/api/tasks/patient-tasks/{task.Id}", task);
    }

    [HttpPatch("patient-tasks/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> UpdatePatientTask(Guid id, [FromBody] UpdatePatientTaskRequest request)
    {
        var command = new UpdatePatientTaskCommand(
            id,
            request.Title,
            request.Notes,
            request.DueDateUtc,
            request.Priority,
            request.Status,
            request.StageDefinitionIdsInOrder);

        var updatedTask = await mediator.Send(command);
        
        return Ok(updatedTask);
    }

    [HttpPost("patient-tasks/{patientTaskId:guid}/assign/{userId:guid}")]
    [ProducesResponseType(typeof(List<PatientTaskAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> AssignPatientTask(Guid patientTaskId, Guid userId, CancellationToken ct)
    {
        var assignedByUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var result = await mediator.Send(new AssignPatientTaskCommand(patientTaskId, userId, assignedByUserId), ct);

        return Ok(result);
    }

    [HttpDelete("patient-tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> DeletePatientTask(Guid id)
    {
        var command = new DeletePatientTaskCommand(id);

        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
    
    [HttpGet("patient-tasks/")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> GetAllPatientTasks()
    {
        var query = new GetAllPatientTasksQuery();
        var tasks = await mediator.Send(query);

        return Ok(tasks);
    }

    [HttpGet("patient-tasks/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> GetPatientTaskById(Guid id)
    {
        var query = new GetPatientTaskByIdQuery(id);
        var task = await mediator.Send(query);

        return Ok(task);
    }
    
    [HttpGet("patient-tasks/user/self")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskResponse>), StatusCodes.Status200OK)]
    [Authorize]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> GetAllPatientTasksForCurrentUser(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var query = new GetAllPatientTasksForUserQuery(userId);
        var tasks = await mediator.Send(query, ct);

        return Ok(tasks);
    }

    [HttpGet("patient-tasks/user/{userId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> GetAllPatientTasksForUser(Guid userId, CancellationToken ct)
    {
        var query = new GetAllPatientTasksForUserQuery(userId);
        var tasks = await mediator.Send(query, ct);

        return Ok(tasks);
    }

    [HttpGet("patient-tasks/patient/{patientId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Tasks")]
    public async Task<IActionResult> GetAllPatientTasksForPatient(Guid patientId, CancellationToken ct)
    {
        var query = new GetAllPatientTasksForPatientQuery(patientId);
        var tasks = await mediator.Send(query, ct);

        return Ok(tasks);
    }
}