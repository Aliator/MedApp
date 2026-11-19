using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consults.Commands.CloseConsult;
using MedApp.Application.Consults.Commands.ReplyToConsult;
using MedApp.Application.Consults.Commands.RequestConsult;
using MedApp.Application.Consults.Commands.WithdrawConsult;
using MedApp.Application.Consults.Queries.GetIncomingConsults;
using MedApp.Application.Consults.Queries.GetPathwayConsults;
using MedApp.Contracts.Consults.Requests;
using MedApp.Contracts.Consults.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Consults between clinics, and with the lab (MC25 of <c>docs/multi-clinic-plan.md</c>): a pathway's own team sends
/// another team a question; the asked team reads the pathway, read only, while the consult is open, and replies; the
/// asking team closes it after the reply, or withdraws it before one. Anyone who sees patients reads them (each read
/// logged); holders of WorkPathways send, reply, close and withdraw. Nothing is suggested.
/// </summary>
[ApiController]
[Route("api/consults")]
[Tags("Consults")]
public sealed class ConsultsController(IMediator mediator) : ControllerBase
{
    /// <summary>A pathway's consults, newest first, and whether the caller may send another. Records PathwayViewed.</summary>
    [HttpGet("pathways/{pathwayId:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PathwayConsultsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForPathway(Guid pathwayId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPathwayConsultsQuery(pathwayId, User.ToPathwayActor()), ct));

    /// <summary>
    /// The open consults asked of the caller's teams, in one of them or all, oldest first. Records PatientListed for each
    /// patient.
    /// </summary>
    [HttpGet("incoming")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<ConsultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIncoming([FromQuery] Guid? teamId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetIncomingConsultsQuery(teamId, User.ToPathwayActor()), ct));

    /// <summary>Sends a consult about one of the caller's team's pathways to another team, with the question.</summary>
    [HttpPost]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(ConsultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Send([FromBody] RequestConsultRequest request, CancellationToken ct)
    {
        var consult = await mediator.Send(
            new RequestConsultCommand(request.PathwayId, request.AskedTeamId, request.Question, User.ToPathwayActor()), ct);
        return Created($"/api/consults/pathways/{consult.PathwayId}", consult);
    }

    /// <summary>The asked team's reply. Whoever sent it and the pathway's assignees are told.</summary>
    [HttpPost("{id:guid}/reply")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(ConsultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplyToConsultRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReplyToConsultCommand(id, request.Reply, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Closes a replied consult, ending the asked team's reading. Allowed while the record is restricted.</summary>
    [HttpPost("{id:guid}/close")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(ConsultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] ChangeConsultRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new CloseConsultCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Withdraws a consult before its reply, ending the asked team's reading. Allowed while the record is restricted.</summary>
    [HttpPost("{id:guid}/withdraw")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(ConsultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] ChangeConsultRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawConsultCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
