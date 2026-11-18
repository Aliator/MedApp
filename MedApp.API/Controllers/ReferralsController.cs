using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Referrals.Commands.AcceptReferral;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.PauseClock;
using MedApp.Application.Referrals.Commands.ResumeClock;
using MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;
using MedApp.Application.Referrals.Commands.RedirectReferral;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Referrals.Commands.RejectReferral;
using MedApp.Application.Referrals.Commands.RequestReferralInformation;
using MedApp.Application.Referrals.Commands.WithdrawReferral;
using MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;
using MedApp.Application.Referrals.Queries.GetReferralById;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
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
/// marked entered in error. Triage (accept, reject, redirect) needs TriageReferrals and membership of the referral's
/// specialty.
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
            User.GetUserId(),
            request.BreachedOnly,
            request.Paused,
            request.ClockStopped), ct));

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

    /// <summary>
    /// Triage: accepts a referral awaiting triage into a new pathway, from a template of its specialty, with the
    /// priority the triager chose; its target date is the waiting-time clock's. Needs membership of its specialty too
    /// (403). 201, with the pathway.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [RequirePermission(Permission.TriageReferrals)]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptReferral(Guid id, [FromBody] AcceptReferralRequest request, CancellationToken ct)
    {
        var pathway = await mediator.Send(new AcceptReferralCommand(
            id,
            request.TemplateId,
            request.Priority,
            request.Title,
            request.AssigneeUserId,
            request.Notes,
            request.ExpectedVersion,
            User.ToPathwayActor()), ct);

        return Created($"/api/pathways/patient-pathways/{pathway.Id}", pathway);
    }

    /// <summary>Triage: rejects a referral awaiting triage, for a reason (a detail is required for Other).</summary>
    [HttpPost("{id:guid}/reject")]
    [RequirePermission(Permission.TriageReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectReferral(Guid id, [FromBody] RejectReferralRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RejectReferralCommand(
            id, request.Reason, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Triage: redirects a referral awaiting triage, with a detail: to another specialty here (it stays awaiting triage)
    /// or elsewhere (it closes).
    /// </summary>
    [HttpPost("{id:guid}/redirect")]
    [RequirePermission(Permission.TriageReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RedirectReferral(Guid id, [FromBody] RedirectReferralRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RedirectReferralCommand(
            id, request.SpecialtyId, request.Elsewhere, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Triage: asks the referrer for more information. The referral stays open, in the Information requested view, with
    /// its clock still running; the letter asking is written from its page.
    /// </summary>
    [HttpPost("{id:guid}/ask-for-information")]
    [RequirePermission(Permission.TriageReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AskForInformation(Guid id, [FromBody] ReferralVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetReferralInformationRequestedCommand(
            id, Asked: true, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Triage: records that the information asked for came; the referral is back awaiting triage.</summary>
    [HttpPost("{id:guid}/information-received")]
    [RequirePermission(Permission.TriageReferrals)]
    [ProducesResponseType(typeof(ReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InformationReceived(Guid id, [FromBody] ReferralVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetReferralInformationRequestedCommand(
            id, Asked: false, request.ExpectedVersion, User.ToPathwayActor()), ct));

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

    /// <summary>
    /// Pauses an accepted referral's waiting-time clock today, for a delay the patient chose or caused, until a review
    /// date (tomorrow to 90 days ahead), which becomes the pathway's target date. Allowed while the record is restricted.
    /// Returns the pathway's "Referral and wait" card.
    /// </summary>
    [HttpPost("{id:guid}/clock/pause")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(PathwayReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PauseClock(Guid id, [FromBody] PauseClockRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new PauseClockCommand(
            id, request.Reason, request.Detail, request.ReviewOn, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Resumes a referral's paused waiting-time clock today; the pathway's target date goes back to the clock's.</summary>
    [HttpPost("{id:guid}/clock/resume")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(PathwayReferralResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeClock(Guid id, [FromBody] ResumeClockRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ResumeClockCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
