using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Referrals.Commands.WithdrawReferral;
using MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Requests;
using MedApp.Contracts.Referrals.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Referrals: requests from referrers (GP practices) for the centre to see a patient in a specialty, registered from
/// the letter by holders of RegisterReferrals and read by anyone who can see patients. Every change is recorded in the
/// referral's history, in the same save; every change carries the version the caller saw (<c>expectedVersion</c>).
/// A restricted or anonymised record's referrals can't be registered or edited (409); they can still be withdrawn or
/// marked entered in error. Triage comes in a later release.
/// </summary>
[ApiController]
[Route("api/referrals")]
[Tags("Referrals")]
public sealed class ReferralsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Registers a referral for a patient already in MedApp. A 400 if the patient has an open referral to the same
    /// specialty, unless <c>confirmPossibleDuplicate</c>.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permission.RegisterReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterReferral([FromBody] RegisterReferralRequest request, CancellationToken ct)
    {
        var referral = await mediator.Send(new RegisterReferralCommand(
            request.PatientId,
            request.SpecialtyId,
            request.ReceivedOn,
            request.ReferrerPractice,
            request.ReferrerClinician,
            request.ReferrerOdsCode,
            request.RequestedPriority,
            request.Reason,
            request.ConfirmPossibleDuplicate,
            User.ToPathwayActor(),
            request.PracticeId), ct);

        return Created($"/api/referrals/{referral.Id}", referral);
    }

    /// <summary>One page of referrals. By default the oldest received first, so the longest wait is at the top.</summary>
    [HttpGet("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<ReferralSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchReferrals([FromQuery] SearchReferralsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchReferralsQuery(
            request.Page,
            request.PageSize,
            request.Sort,
            request.Ascending,
            request.Statuses,
            request.SpecialtyIds,
            request.Mine,
            request.PatientId,
            request.Search,
            User.GetUserId()), ct));

    /// <summary>
    /// How many referrals await triage in the caller's specialties (every specialty's, if they belong to none), for the
    /// side nav. A count only: it names no patients, so it records nothing.
    /// </summary>
    [HttpGet("awaiting-triage/count")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ReferralCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CountAwaitingTriage(CancellationToken ct) =>
        Ok(await mediator.Send(new CountReferralsAwaitingTriageQuery(User.GetUserId()), ct));

    /// <summary>One referral, with its history. Opening it is recorded (<c>ReferralViewed</c>).</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReferral(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetReferralByIdQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Corrects a referral's details, while it awaits triage.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.RegisterReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditReferral(Guid id, [FromBody] EditReferralRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EditReferralCommand(
            id,
            request.SpecialtyId,
            request.ReceivedOn,
            request.ReferrerPractice,
            request.ReferrerClinician,
            request.ReferrerOdsCode,
            request.RequestedPriority,
            request.Reason,
            request.ExpectedVersion,
            User.ToPathwayActor(),
            request.PracticeId), ct));

    /// <summary>Withdraws a referral awaiting triage (the referrer or the patient withdrew it), with a detail.</summary>
    [HttpPost("{id:guid}/withdraw")]
    [RequirePermission(Permission.RegisterReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawReferral(Guid id, [FromBody] CloseReferralRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawReferralCommand(id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Marks a referral awaiting triage entered in error (on the wrong patient, or registered twice), with a detail.</summary>
    [HttpPost("{id:guid}/entered-in-error")]
    [RequirePermission(Permission.RegisterReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] CloseReferralRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkReferralEnteredInErrorCommand(id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
