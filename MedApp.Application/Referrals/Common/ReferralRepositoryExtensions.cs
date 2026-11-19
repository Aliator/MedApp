using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

public static class ReferralRepositoryExtensions
{
    /// <summary>What a refusal across a team wall names a referral as (<c>TeamBoundaryRefused</c>'s <c>targetKind</c>).</summary>
    public const string Target = "Referral";

    /// <summary>What the 409 to another team's change calls it (<see cref="TeamWalls.RecordNotYours"/>).</summary>
    public const string What = "This referral";

    /// <summary>The words of a missing referral's 404, and of one behind the viewer's team wall (the same, decision 204).</summary>
    public static string NotFoundMessage(Guid id) => $"Referral '{id}' was not found.";

    /// <summary>
    /// Why the viewer sees a referral (MC5, the light demo's <c>findReferral</c>): its current team is one of theirs, or
    /// its pathway is seen once it's accepted, or they see every team; otherwise a 404 with a missing one's words,
    /// recorded (<c>TeamBoundaryRefused</c>). Redirected to another team, the old team stops seeing it.
    /// </summary>
    public static Task<TeamAccessKind> EnsureReferralVisibleAsync(
        this ITeamBoundary boundary, Referral referral, TeamScope scope, CancellationToken ct) =>
        boundary.EnsureRecordVisibleAsync(
            Target, referral.Id, referral.TeamId, referral.PathwayId, scope, NotFoundMessage(referral.Id), ct);

    /// <summary>
    /// The referral, tracked, for a change by its own team (the demo's <c>referralToChange</c>): behind the viewer's
    /// wall a 404 (recorded); seen but another team's (an admin's, the lab's through the pathway) a 409
    /// (<see cref="TeamWalls.RecordNotYours"/>).
    /// </summary>
    public static async Task<Referral> GetForChangeOrThrowAsync(
        this IReferralRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var referral = await GetForUpdateOrThrowAsync(repository, id, ct);
        await boundary.EnsureReferralVisibleAsync(referral, scope, ct);
        await boundary.EnsureCanChangeTeamAsync(referral.TeamId, scope, What, ct);
        return referral;
    }

    /// <summary>
    /// The referral, tracked for changes, or a 404; the team walls as <see cref="GetForChangeOrThrowAsync"/>; then a
    /// 409 unless its details can be edited now (<see cref="ReferralWorkflow.WhyCantEdit"/>: it awaits triage, and its
    /// patient's record isn't restricted or anonymised).
    /// </summary>
    public static async Task<Referral> GetForEditOrThrowAsync(
        this IReferralRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var referral = await repository.GetForChangeOrThrowAsync(id, boundary, scope, ct);
        if (ReferralWorkflow.WhyCantEdit(referral) is { } why)
            throw new ConflictException(why);
        return referral;
    }

    /// <summary>
    /// The referral, tracked for changes, to withdraw it or mark it entered in error, or a 404; the team walls as
    /// <see cref="GetForChangeOrThrowAsync"/>; then a 409 unless it awaits triage and its record isn't anonymised.
    /// Allowed while the record is restricted, so care can be stopped safely (<see cref="ReferralWorkflow.WhyCantClose"/>).
    /// </summary>
    public static async Task<Referral> GetForCloseOrThrowAsync(
        this IReferralRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var referral = await repository.GetForChangeOrThrowAsync(id, boundary, scope, ct);
        if (ReferralWorkflow.WhyCantClose(referral) is { } why)
            throw new ConflictException(why);
        return referral;
    }

    /// <summary>
    /// The referral, tracked for changes, for <paramref name="actor"/> to triage it, or a 404 (also behind their team
    /// wall, recorded); a 403 unless they hold <c>TriageReferrals</c> and belong to its team (triage's own rule and
    /// words, which keep it to its own team); a 409 once it's been decided (naming who and how), while its record is
    /// restricted or anonymised, or if it changed since <paramref name="expectedVersion"/>
    /// (<see cref="ReferralWorkflow.EnsureCanTriage"/>).
    /// </summary>
    public static async Task<Referral> GetForTriageOrThrowAsync(
        this IReferralRepository repository,
        Guid id,
        PathwayActor actor,
        int expectedVersion,
        ITeamBoundary boundary,
        CancellationToken ct)
    {
        var referral = await GetForUpdateOrThrowAsync(repository, id, ct);
        await boundary.EnsureReferralVisibleAsync(referral, actor.Teams, ct);
        var isMember = actor.Has(Permission.TriageReferrals)
                       && await repository.IsTeamMemberAsync(actor.UserId, referral.TeamId, ct);
        ReferralWorkflow.EnsureCanTriage(referral, actor, isMember, expectedVersion);
        return referral;
    }

    /// <summary>
    /// The conflict to report when a triage's save was refused because the referral changed meanwhile: if someone
    /// decided it first, who and how (<see cref="ReferralWorkflow.AlreadyTriagedMessage"/>); otherwise that it changed.
    /// </summary>
    public static async Task<ConflictException> TriageConflictAsync(this IReferralRepository repository, Guid id, CancellationToken ct)
    {
        var now = await repository.GetByIdAsync(id, ct);
        return now is not null && !ReferralWorkflow.IsOpen(now.Status)
            ? new ConflictException(ReferralWorkflow.AlreadyTriagedMessage(now))
            : new ConflictException(ReferralWorkflow.ChangedMeanwhileMessage);
    }

    /// <summary>The referral, read-only, as the viewer sees it, with why they do; a 404 when it's missing or behind their wall (recorded).</summary>
    public static async Task<(Referral Referral, TeamAccessKind Access)> GetVisibleOrThrowAsync(
        this IReferralRepository repository, Guid id, ITeamBoundary boundary, TeamScope scope, CancellationToken ct)
    {
        var referral = await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);
        return (referral, await boundary.EnsureReferralVisibleAsync(referral, scope, ct));
    }

    private static async Task<Referral> GetForUpdateOrThrowAsync(IReferralRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);

    private static NotFoundException NotFound(Guid id) => new(NotFoundMessage(id));
}
