using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Commands.DeferMdtItem;
using MedApp.Application.Mdt.Commands.ListForMdt;
using MedApp.Application.Mdt.Commands.RecordMdtOutcome;
using MedApp.Application.Mdt.Commands.WithdrawMdtItem;
using MedApp.Application.Mdt.Queries.GetMdtItemsAwaitingMeeting;
using MedApp.Application.Mdt.Queries.GetMdtMeetingList;
using MedApp.Application.Mdt.Queries.GetMdtMeetings;
using MedApp.Application.Mdt.Queries.GetPathwayMdtItems;
using MedApp.Contracts.Mdt.Requests;
using MedApp.Contracts.Mdt.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The MDT list (<c>docs/centre-phases-3-5-plan.md</c>, decisions 141 to 144): pathways put forward for a team's
/// multidisciplinary team meeting (a calendar meeting marked as its MDT), with the question for the team and, once
/// discussed, the outcome as a person wrote it. Anyone who sees patients reads the lists (each read logged); holders of
/// WorkPathways list, defer and withdraw; holders of RecordMdtOutcomes record the outcome. MedApp never suggests one.
/// </summary>
[ApiController]
[Route("api/mdt")]
[Tags("MDT")]
public sealed class MdtController(IMediator mediator) : ControllerBase
{
    /// <summary>The MDTs from 60 days ago to 180 days ahead, in one team or all, by start. Names no patient.</summary>
    [HttpGet("meetings")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<MdtMeetingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMeetings([FromQuery] Guid? teamId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMdtMeetingsQuery(teamId, User.ToTeamScope()), ct));

    /// <summary>One MDT's list, in the order items were listed. Records PatientListed for each patient on it.</summary>
    [HttpGet("meetings/{meetingId:guid}/items")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(MdtMeetingListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeetingList(Guid meetingId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMdtMeetingListQuery(meetingId, User.ToPathwayActor()), ct));

    /// <summary>Open items whose MDT was cancelled, to list again, in one team or all. Records PatientListed for each.</summary>
    [HttpGet("awaiting-meeting")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<MdtItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAwaitingMeeting([FromQuery] Guid? teamId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMdtItemsAwaitingMeetingQuery(teamId, User.ToPathwayActor()), ct));

    /// <summary>A pathway's MDT items, newest listed first. Records PathwayViewed.</summary>
    [HttpGet("pathways/{pathwayId:guid}/items")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<MdtItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPathwayItems(Guid pathwayId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPathwayMdtItemsQuery(pathwayId, User.ToPathwayActor()), ct));

    /// <summary>Lists a pathway for an MDT that hasn't ended, with the question for the team.</summary>
    [HttpPost("items")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(MdtItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ListForMdt([FromBody] ListForMdtRequest request, CancellationToken ct)
    {
        var item = await mediator.Send(
            new ListForMdtCommand(request.PathwayId, request.MeetingId, request.Question, User.ToPathwayActor()), ct);
        return Created($"/api/mdt/pathways/{item.PathwayId}/items", item);
    }

    /// <summary>Takes an item off the list without discussion. Allowed while the record is restricted.</summary>
    [HttpPost("items/{id:guid}/withdraw")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(MdtItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawMdtItemRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawMdtItemCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Moves an item to a later MDT, or lists it again after its meeting was cancelled.</summary>
    [HttpPost("items/{id:guid}/defer")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(MdtItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Defer(Guid id, [FromBody] DeferMdtItemRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new DeferMdtItemCommand(id, request.MeetingId, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Records the team's outcome as written, and who was present, once its meeting has started. The pathway's
    /// assignees are told. Allowed while the record is restricted.
    /// </summary>
    [HttpPost("items/{id:guid}/outcome")]
    [RequirePermission(Permission.RecordMdtOutcomes)]
    [ProducesResponseType(typeof(MdtItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordOutcome(Guid id, [FromBody] RecordMdtOutcomeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordMdtOutcomeCommand(
            id, request.Outcome, request.PresentUserIds ?? [], request.ExpectedVersion, User.ToPathwayActor()), ct));
}
