using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

/// <summary>
/// One page of referrals, filtered and sorted (see <see cref="ReferralSearch"/>). <paramref name="Mine"/> limits it to
/// the teams <paramref name="ViewerId"/> belongs to (every team, if they belong to none).
/// <paramref name="BreachedOnly"/>, <paramref name="Paused"/> and <paramref name="ClockStopped"/> filter by the
/// waiting-time clock (the Waiting list is accepted with the clock not stopped; Paused clocks is paused).
/// </summary>
public sealed record SearchReferralsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    IReadOnlyList<string>? Statuses,
    IReadOnlyList<Guid>? TeamIds,
    bool Mine,
    Guid? PatientId,
    string? Search,
    Guid ViewerId,
    bool BreachedOnly = false,
    bool? Paused = null,
    bool? ClockStopped = null)
    : IRequest<PagedResponse<ReferralSummaryResponse>>;
