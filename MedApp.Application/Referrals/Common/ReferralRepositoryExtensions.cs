using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

public static class ReferralRepositoryExtensions
{
    /// <summary>
    /// The referral, tracked for changes, or a 404; a 409 unless its details can be edited now
    /// (<see cref="ReferralWorkflow.WhyCantEdit"/>: it awaits triage, and its patient's record isn't restricted or
    /// anonymised).
    /// </summary>
    public static async Task<Referral> GetForEditOrThrowAsync(this IReferralRepository repository, Guid id, CancellationToken ct)
    {
        var referral = await GetForUpdateOrThrowAsync(repository, id, ct);
        if (ReferralWorkflow.WhyCantEdit(referral) is { } why)
            throw new ConflictException(why);
        return referral;
    }

    /// <summary>
    /// The referral, tracked for changes, to withdraw it or mark it entered in error, or a 404; a 409 unless it awaits
    /// triage and its record isn't anonymised. Allowed while the record is restricted, so care can be stopped safely
    /// (<see cref="ReferralWorkflow.WhyCantClose"/>).
    /// </summary>
    public static async Task<Referral> GetForCloseOrThrowAsync(this IReferralRepository repository, Guid id, CancellationToken ct)
    {
        var referral = await GetForUpdateOrThrowAsync(repository, id, ct);
        if (ReferralWorkflow.WhyCantClose(referral) is { } why)
            throw new ConflictException(why);
        return referral;
    }

    /// <summary>
    /// The referral, tracked for changes, for <paramref name="actor"/> to triage it, or a 404; a 403 unless they hold
    /// <c>TriageReferrals</c> and belong to its team; a 409 once it's been decided (naming who and how), while its
    /// record is restricted or anonymised, or if it changed since <paramref name="expectedVersion"/>
    /// (<see cref="ReferralWorkflow.EnsureCanTriage"/>).
    /// </summary>
    public static async Task<Referral> GetForTriageOrThrowAsync(
        this IReferralRepository repository, Guid id, PathwayActor actor, int expectedVersion, CancellationToken ct)
    {
        var referral = await GetForUpdateOrThrowAsync(repository, id, ct);
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

    public static async Task<Referral> GetOrThrowAsync(this IReferralRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);

    private static async Task<Referral> GetForUpdateOrThrowAsync(IReferralRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);

    private static NotFoundException NotFound(Guid id) => new($"Referral '{id}' was not found.");
}
