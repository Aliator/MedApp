using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Hospitals.Requests;
using MedApp.Application.Hospitals.Setup;
using MedApp.Contracts.Common;
using MedApp.Contracts.Hospitals.Requests;
using MedApp.Contracts.Hospitals.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Hospitals (MC18 of <c>docs/multi-clinic-plan.md</c>): organisation-wide setup, kept by holders of ManageSetup and read
/// by everyone (UseApp). One that sent requests is archived, not deleted.
/// </summary>
[ApiController]
[Route("api/hospitals")]
[Tags("Hospitals")]
public sealed class HospitalsController(IMediator mediator) : ControllerBase
{
    /// <summary>Every hospital by name; archived ones only with <c>includeArchived</c>.</summary>
    [HttpGet]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<HospitalResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetHospitalsQuery(includeArchived), ct));

    [HttpPost]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] HospitalDetailsRequest request, CancellationToken ct)
    {
        var hospital = await mediator.Send(new CreateHospitalCommand(
            request.Name, request.OdsCode, request.Contact, request.ReportDeliveryMethod, request.DefaultDeadlineDays), ct);
        return Created($"/api/hospitals/{hospital.Id}", hospital);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] HospitalDetailsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateHospitalCommand(
            id, request.Name, request.OdsCode, request.Contact, request.ReportDeliveryMethod, request.DefaultDeadlineDays), ct));

    /// <summary>Deletes a hospital that sent no requests; one that did is a 409 (archive it instead).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteHospitalCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetHospitalArchivedCommand(id, true), ct));

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(HospitalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetHospitalArchivedCommand(id, false), ct));
}

/// <summary>
/// Hospital requests (MC18): typed in by the lab team (ProcessSpecimens), each bringing its sample and opening its lab
/// case with no pathway. The lab and admins see them; anyone else gets a 404 for one and an empty list. POSTs for the
/// search and the patient matches, so no NHS number is in a URL.
/// </summary>
[ApiController]
[Route("api/hospital-requests")]
[Tags("Hospital requests")]
public sealed class HospitalRequestsController(IMediator mediator) : ControllerBase
{
    /// <summary>Registers a hospital's request: the patient chosen from the matches or new, its sample and its lab case.</summary>
    [HttpPost]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(HospitalRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] CreateHospitalRequestRequest request, CancellationToken ct)
    {
        var registered = await mediator.Send(new RegisterHospitalRequestCommand(
            request.HospitalId, request.PatientId, request.NewPatient, request.HospitalPatientNumber, request.NhsNumber,
            request.SurgeonName, request.Priority, request.IsCancerPathway, request.AuthoredOn, request.ReportNeededBy,
            request.CollectedAtUtc, request.SpecimenTypeId, request.LabTestId, User.ToPathwayActor()), ct);
        return Created($"/api/hospital-requests/{registered.Id}", registered);
    }

    /// <summary>One request, with its patient and lab case. Viewing it is recorded (<c>LabCaseViewed</c>).</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(HospitalRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetHospitalRequestQuery(id, User.ToPathwayActor()), ct));

    /// <summary>A page of requests: a hospital, a priority, a search and a column. Each patient shown is recorded as listed.</summary>
    [HttpPost("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<HospitalRequestSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromBody] HospitalRequestSearchRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchHospitalRequestsQuery(
            request.Page, request.PageSize, request.HospitalId, request.Priority, request.Search, request.Sort, request.Ascending,
            User.ToPathwayActor()), ct));

    /// <summary>The patients a request could be for (NHS number, or last name and date of birth), for a person to choose from.</summary>
    [HttpPost("patient-matches")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(IReadOnlyList<HospitalPatientMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatientMatches([FromBody] HospitalPatientMatchRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new FindHospitalPatientMatchesQuery(
            request.NhsNumber, request.LastName, request.DateOfBirth, User.ToPathwayActor()), ct));
}
