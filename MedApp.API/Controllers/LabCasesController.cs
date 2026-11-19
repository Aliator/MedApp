using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Application.Lab.Cases.Queries;
using MedApp.Contracts.Common;
using MedApp.Contracts.Lab.Requests;
using MedApp.Contracts.Lab.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Lab cases (MC15 of <c>docs/multi-clinic-plan.md</c>): the lab's unit of work, one request's samples, its steps and
/// one report. The lab team works them (ProcessSpecimens): Received, Accessioned, "Sample adequate?", the test's steps,
/// "Needs more work?", then the results entered and the report authorised (AuthoriseLabReports). The lab and admins see
/// every case, a clinic its own clinic's. Every change carries the version the caller saw and is audited; MedApp never
/// chooses or suggests an outcome, and never reads or interprets the results.
/// </summary>
[ApiController]
[Route("api/lab-cases")]
[Tags("Lab cases")]
public sealed class LabCasesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// A page of lab cases: a view (tab), the source and test filters, a search (the number, the patient's name or NHS
    /// number, the test, where it came from) and a column. A POST, as the search may be a case number, which stays out of
    /// URLs. Each patient shown is recorded as listed.
    /// </summary>
    [HttpPost("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<LabCaseSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromBody] LabCaseSearchRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchLabCasesQuery(
            request.Page, request.PageSize, request.View, request.Source, request.LabTestId, request.Search, request.Sort,
            request.Ascending, User.ToPathwayActor()), ct));

    /// <summary>One lab case. Opening it is recorded (<c>LabCaseViewed</c>); another clinic's is a 404.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLabCaseQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Opens a lab case by hand for a received sample with none: the lab chooses the test.</summary>
    [HttpPost]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Open([FromBody] OpenLabCaseRequest request, CancellationToken ct)
    {
        var labCase = await mediator.Send(new OpenLabCaseCommand(
            request.SpecimenId, request.LabTestId, request.ExpectedVersion, User.ToPathwayActor()), ct);
        return Created($"/api/lab-cases/{labCase.Id}", labCase);
    }

    /// <summary>Records the case's next step: Accessioned, or the test's step it's at.</summary>
    [HttpPost("{id:guid}/steps")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordStep(Guid id, [FromBody] RecordLabStepRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordLabStepCommand(
            id, request.TestStepId, request.FixedStep, request.AtUtc, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Marks the latest step entered in error (whoever recorded it).</summary>
    [HttpPost("{id:guid}/steps/{stepId:guid}/entered-in-error")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkStepEnteredInError(
        Guid id, Guid stepId, [FromBody] LabCaseCorrectionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkLabStepEnteredInErrorCommand(
            id, stepId, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records a choice's outcome ("Sample adequate?", "Needs more work?"), as the lab member chose it.</summary>
    [HttpPost("{id:guid}/outcomes")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordOutcome(Guid id, [FromBody] RecordLabOutcomeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordLabOutcomeCommand(
            id, request.Choice, request.Outcome, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Enters the results and authorises the report, every step done: the samples are reported, a clinic case's result
    /// goes to the requesting clinician, and a pathway's lab stage gets the results (the lab then finishes it).
    /// </summary>
    [HttpPost("{id:guid}/report")]
    [RequirePermission(Permission.AuthoriseLabReports)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Authorise(Guid id, [FromBody] AuthoriseLabReportRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AuthoriseLabReportCommand(id, request.Results, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Adds a hospital case's surgery details, as the hospital wrote them (MC20): the date, procedure, outcome and other
    /// details, or "no surgery expected" with why (only once the report was sent). Added to a case awaiting them, they
    /// close it. Nothing is worked out from them.
    /// </summary>
    [HttpPost("{id:guid}/surgery-details")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSurgeryDetails(Guid id, [FromBody] AddSurgeryDetailsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AddSurgeryDetailsCommand(
            id, request.SurgeryOn, request.Procedure, request.Outcome, request.NoSurgeryExpected, request.Detail,
            request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Marks the case entered in error: whoever opened it within 24 hours, or a holder of CorrectRecords (checked by
    /// the handler: 403 otherwise). Its samples are freed for the right case.
    /// </summary>
    [HttpPost("{id:guid}/entered-in-error")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(LabCaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] LabCaseCorrectionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkLabCaseEnteredInErrorCommand(id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
