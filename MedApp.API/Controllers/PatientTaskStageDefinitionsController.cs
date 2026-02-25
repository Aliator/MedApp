using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.DeletePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.UpdatePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Requests;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/tasks/patient-task-stage-definitions")]
public sealed class PatientTaskStageDefinitionsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> CreatePatientTaskStageDefinition([FromBody] CreatePatientTaskStageDefinitionRequest request)
    {
        var command = new CreatePatientTaskStageCommand(
            request.Name,
            request.Description,
            request.Instructions);

        var stageDefinition = await mediator.Send(command);

        return Created($"/api/tasks/patient-task-stage-definitions/{stageDefinition.Id}", stageDefinition);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> UpdatePatientTaskStageDefinition(Guid id, [FromBody] UpdatePatientTaskStageDefinitionRequest request)
    {
        var command = new UpdatePatientTaskStageCommand(
            id,
            request.Name,
            request.Description,
            request.Instructions);

        var updatedStageDefinition = await mediator.Send(command);

        return Ok(updatedStageDefinition);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> DeletePatientTaskStageDefinition(Guid id)
    {
        var command = new DeletePatientTaskStageCommand(id);
        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskStageDefinitionResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetAllPatientTaskStageDefinitions()
    {
        var query = new GetAllPatientTaskStagesQuery();
        var stageDefinitions = await mediator.Send(query);

        return Ok(stageDefinitions);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetPatientTaskStageDefinitionById(Guid id)
    {
        var query = new GetPatientTaskStageByIdQuery(id);
        var stageDefinition = await mediator.Send(query);

        return Ok(stageDefinition);
    }
}