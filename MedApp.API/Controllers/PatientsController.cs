using Microsoft.AspNetCore.Mvc;
using MedApp.API.Dtos.Requests;
using MedApp.API.Dtos.Responses;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    public IActionResult CreatePatient([FromBody] CreatePatientRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPatientById(Guid id)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientResponse>), StatusCodes.Status200OK)]
    public IActionResult GetAllPatients()
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
    
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult UpdatePatient(
        Guid id,
        [FromBody] UpdatePatientRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeletePatient(Guid id)
    {
        return StatusCode(StatusCodes.Status501NotImplemented);
    }
}