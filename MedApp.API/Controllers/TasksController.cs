using System.Security.Claims;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Tasks.PatientTasks.Commands.AssignPatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.CreatePatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.DeletePatientTask;
using MedApp.Application.Tasks.PatientTasks.Commands.UpdatePatientTask;
using MedApp.Application.Tasks.PatientTasks.Queries.GetAllPatientTasks;
using MedApp.Application.Tasks.PatientTasks.Queries.GetPatientTaskById;
using MedApp.Contracts.Tasks.PatientTasks.Requests;
using MedApp.Contracts.Tasks.PatientTasks.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/tasks/patient-tasks")]
public sealed class TasksController(IMediator mediator, ISessionService sessionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status201Created)]
    [Authorize(Roles = "Admin")]
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

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
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

        if (updatedTask is null)
        {
            return NotFound();
        }

        return Ok(updatedTask);
    }

    [HttpPost("patient-tasks/{patientTaskId:guid}/assign/{userId:guid}")]
    [ProducesResponseType(typeof(List<PatientTaskAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignPatientTask(Guid patientTaskId, Guid userId, CancellationToken ct)
    {
        var assignedByUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var result = await mediator.Send(new AssignPatientTaskCommand(patientTaskId, userId, assignedByUserId), ct);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
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
    
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllPatientTasks()
    {
        var query = new GetAllPatientTasksQuery();
        var tasks = await mediator.Send(query);

        return Ok(tasks);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPatientTaskById(Guid id)
    {
        var query = new GetPatientTaskByIdQuery(id);
        var task = await mediator.Send(query);

        if (task is null)
        {
            return NotFound();
        }

        return Ok(task);
    }
}