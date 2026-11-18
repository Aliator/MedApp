using MedApp.Application.Common.Exceptions;
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

    public static async Task<Referral> GetOrThrowAsync(this IReferralRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);

    private static async Task<Referral> GetForUpdateOrThrowAsync(IReferralRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);

    private static NotFoundException NotFound(Guid id) => new($"Referral '{id}' was not found.");
}
