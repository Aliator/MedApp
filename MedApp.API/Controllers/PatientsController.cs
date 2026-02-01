using Microsoft.AspNetCore.Mvc;
using MedApp.API.Dtos.Requests;
using MedApp.API.Dtos.Responses;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Queries.GetAllPatients;
using MedApp.Application.Patients.Queries.GetPatientById;
using MediatR;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
    {
        var command = new CreatePatientCommand(
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Email
        );

        var id = await mediator.Send(command);

        var response = new PatientResponse
        {
            Id = id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Email = request.Email
        };

        return CreatedAtAction(nameof(GetPatientById), new { id }, response);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientById(Guid id)
    {
        var query = new GetPatientByIdQuery(id);

        var patient = await mediator.Send(query);

        if (patient is null)
            return NotFound();

        return Ok(patient);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPatients()
    {
        var query = new GetAllPatientsQuery();

        var patients = await mediator.Send(query);

        return Ok(patients);
    }
    
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePatient(
        Guid id,
        [FromBody] UpdatePatientRequest request)
    {
        var command = new UpdatePatientCommand(
            id,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Email
        );

        var patient = await mediator.Send(command);

        if (patient is null)
            return NotFound();

        return Ok(patient);
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePatient(Guid id)
    {
        var command = new DeletePatientCommand(id);

        var deleted = await mediator.Send(command);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}