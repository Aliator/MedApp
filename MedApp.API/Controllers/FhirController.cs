using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.API.Fhir;
using MedApp.Application.Fhir.Queries.GetFhirCapabilityStatement;
using MedApp.Application.Fhir.Queries.GetFhirPatient;
using MedApp.Application.Fhir.Queries.SearchFhirPatients;
using MedApp.Contracts.Fhir;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// A read-only FHIR R4 API, in the UK Core profiles' shape: patients only, by ID or NHS number. Responses are
/// <c>application/fhir+json</c> (a request for another format is a 406), never cached; errors are OperationOutcomes,
/// an unknown path under it included (<see cref="FhirJson.NotFoundAsync"/>). For staff, as the patient pages are, and
/// recorded in the activity log as they are; the CapabilityStatement (<c>metadata</c>) is open to anyone.
/// </summary>
[ApiController]
[Route("api/fhir")]
[TypeFilter<FhirExceptionFilter>]
[TypeFilter<FhirFormatFilter>]
[Tags("FHIR")]
[RequirePermission(Permission.ViewPatients)]
public sealed class FhirController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// What the API can do, as a CapabilityStatement, which FHIR clients read first. Open to anyone, as FHIR expects:
    /// it says nothing about any patient.
    /// </summary>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(FhirCapabilityStatement), StatusCodes.Status200OK, FhirJson.ContentType)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status406NotAcceptable, FhirJson.ContentType)]
    [AllowAnonymous]
    public async Task<IActionResult> Metadata(CancellationToken ct) =>
        new FhirResult<FhirCapabilityStatement>(await mediator.Send(new GetFhirCapabilityStatementQuery(), ct));

    /// <summary>A patient as a UK Core Patient resource. 404 (an OperationOutcome) for an unknown ID.</summary>
    [HttpGet("Patient/{id}")]
    [ProducesResponseType(typeof(FhirPatient), StatusCodes.Status200OK, FhirJson.ContentType)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound, FhirJson.ContentType)]
    public async Task<IActionResult> ReadPatient(string id, CancellationToken ct) =>
        new FhirResult<FhirPatient>(await mediator.Send(new GetFhirPatientQuery(id), ct));

    /// <summary>
    /// Patients by NHS number: <c>identifier=https://fhir.nhs.uk/Id/nhs-number|{number}</c>, as a searchset Bundle. 400
    /// (an OperationOutcome) without an identifier, or for a number that isn't a valid NHS number.
    /// </summary>
    [HttpGet("Patient")]
    [ProducesResponseType(typeof(FhirBundle), StatusCodes.Status200OK, FhirJson.ContentType)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status400BadRequest, FhirJson.ContentType)]
    public async Task<IActionResult> SearchPatients([FromQuery] string? identifier, CancellationToken ct) =>
        new FhirResult<FhirBundle>(await mediator.Send(new SearchFhirPatientsQuery(identifier, FhirBaseUrl()), ct));

    /// <summary>The FHIR API's own address, e.g. <c>https://host/api/fhir</c>.</summary>
    private string FhirBaseUrl() => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/fhir";
}
