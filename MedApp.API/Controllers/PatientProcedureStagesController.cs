using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/procedures/patient-procedure-stage")]
public sealed class PatientProcedureStagesController(IMediator mediator) : ControllerBase
{
    [HttpPost("definitions")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> CreatePatientProcedureStageDefinition([FromBody] CreatePatientProcedureStageDefinitionRequest request)
    {
        var command = new CreatePatientProcedureStageDefinitionCommand(
            request.Name,
            request.Description,
            request.Instructions);

        var stageDefinition = await mediator.Send(command);

        return Created($"/api/procedures/patient-procedure-stage-definitions/{stageDefinition.Id}", stageDefinition);
    }

    [HttpPatch("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> UpdatePatientProcedureStageDefinition(Guid id, [FromBody] UpdatePatientProcedureStageDefinitionRequest request)
    {
        var command = new UpdatePatientProcedureStageDefinitionCommand(
            id,
            request.Name,
            request.Description,
            request.Instructions);

        var updatedStageDefinition = await mediator.Send(command);

        return Ok(updatedStageDefinition);
    }

    [HttpDelete("definitions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> DeletePatientProcedureStageDefinition(Guid id)
    {
        var command = new DeletePatientProcedureStageDefinitionCommand(id);
        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureStageDefinitionResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetAllPatientProcedureStageDefinitions()
    {
        var query = new GetAllPatientProcedureStageDefinitionsQuery();
        var stageDefinitions = await mediator.Send(query);

        return Ok(stageDefinitions);
    }

    [HttpGet("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetPatientProcedureStageDefinitionById(Guid id)
    {
        var query = new GetPatientProcedureStageDefinitionByIdQuery(id);
        var stageDefinition = await mediator.Send(query);

        return Ok(stageDefinition);
    }
    
    [HttpPost("templates")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> CreatePatientProcedureStageTemplate([FromBody] CreatePatientProcedureStageTemplateRequest request)
    {
        var command = new CreatePatientProcedureTemplateCommand(
            request.Name,
            request.StageDefinitionIdsInOrder);

        var template = await mediator.Send(command);

        return Created($"/api/procedures/patient-procedure-stage/templates/{template.Id}", template);
    }

    [HttpPatch("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> UpdatePatientProcedureStageTemplate(Guid id, [FromBody] UpdatePatientProcedureStageTemplateRequest request)
    {
        var command = new UpdatePatientProcedureTemplateCommand(
            id,
            request.Name,
            request.StageDefinitionIdsInOrder);

        var updatedTemplate = await mediator.Send(command);

        return Ok(updatedTemplate);
    }

    [HttpDelete("templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> DeletePatientProcedureStageTemplate(Guid id)
    {
        var command = new DeletePatientProcedureTemplateCommand(id);
        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureStageTemplateResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> GetAllPatientProcedureStageTemplates()
    {
        var query = new GetAllPatientProcedureTemplatesQuery();
        var templates = await mediator.Send(query);

        return Ok(templates);
    }

    [HttpGet("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> GetPatientProcedureStageTemplateById(Guid id)
    {
        var query = new GetPatientProcedureTemplateByIdQuery(id);
        var template = await mediator.Send(query);

        return Ok(template);
    }
    
}