using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Repositories;

/// <summary>
/// Referrals, with their patient and specialty, and their history. Every change saves its history entries in the
/// same save, so the history can't miss one. It shares the request's DbContext with the pathway repository, so a
/// later change that touches both (accepting a referral into a new pathway) can save them together.
/// </summary>
public interface IReferralRepository
{
    /// <summary>A referral with its patient and specialty, read-only.</summary>
    Task<Referral?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>A referral with its patient and specialty, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Referral?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a new referral and its history entries.</summary>
    Task AddAsync(Referral referral, IEnumerable<ReferralEvent> events, CancellationToken ct);

    /// <summary>
    /// Saves changes to referrals loaded with <see cref="GetForUpdateAsync"/>, with their history entries. If someone else
    /// saved one since it was loaded (its row version changed), nothing is saved and it's a 409
    /// (<see cref="ReferralWorkflow.ChangedMeanwhileMessage"/>).
    /// </summary>
    Task SaveAsync(IEnumerable<ReferralEvent> events, CancellationToken ct);

    /// <summary>
    /// Adds history entries to the request's DbContext without saving: the next save of the context (accepting saves
    /// the referral and its entries with the new pathway, through the pathway repository) saves them too.
    /// </summary>
    void AddEvents(IEnumerable<ReferralEvent> events);

    /// <summary>Whether a member of staff belongs to a specialty (triage needs it).</summary>
    Task<bool> IsSpecialtyMemberAsync(Guid userId, Guid specialtyId, CancellationToken ct);

    /// <summary>The referral accepted into a pathway, with its specialty, read-only; null if none was.</summary>
    Task<Referral?> GetForPathwayAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>A referral's history, oldest first.</summary>
    Task<IReadOnlyList<ReferralEvent>> GetHistoryAsync(Guid referralId, CancellationToken ct);

    /// <summary>How many files (not deleted) are attached to the referral.</summary>
    Task<int> CountLettersAsync(Guid referralId, CancellationToken ct);

    /// <summary>
    /// The patient's open referrals to a specialty, for the duplicate check: awaiting triage, or accepted with their
    /// pathway still under way.
    /// </summary>
    Task<IReadOnlyList<Referral>> GetOpenForPatientAsync(Guid patientId, Guid specialtyId, CancellationToken ct);

    /// <summary>One page of referrals matching the query (see <see cref="ReferralSearch"/>), with their patients and specialties.</summary>
    Task<PagedResult<ReferralRow>> SearchAsync(SearchReferralsQuery query, IReadOnlyCollection<Guid>? specialtyIds, CancellationToken ct);

    /// <summary>How many referrals await triage, in any of these specialties (every specialty when null).</summary>
    Task<int> CountAwaitingTriageAsync(IReadOnlyCollection<Guid>? specialtyIds, CancellationToken ct);

    /// <summary>A patient's referrals awaiting triage, oldest first, with their specialties.</summary>
    Task<IReadOnlyList<Referral>> GetAwaitingTriageForPatientAsync(Guid patientId, CancellationToken ct);

    /// <summary>
    /// A page of a patient's referral history for their timeline and one entry more, if there is one (see
    /// <see cref="ReferralTimelineSearch"/>), each with what the referral holds now.
    /// </summary>
    Task<IReadOnlyList<ReferralTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct);
}
