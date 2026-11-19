using MedApp.Application.Common.Teams;
using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

/// <summary>
/// One page of referrals, filtered and sorted (see <see cref="ReferralSearch"/>). <paramref name="Mine"/> limits it to
/// the teams <paramref name="ViewerId"/> belongs to (every team, if they belong to none).
/// <paramref name="BreachedOnly"/>, <paramref name="Paused"/> and <paramref name="ClockStopped"/> filter by the
/// waiting-time clock (the Waiting list is accepted with the clock not stopped; Paused clocks is paused).
/// <para>
/// The team walls (MC5): only referrals the viewer may see (<paramref name="Scope"/>: their current team's, or their
/// pathway's once accepted; admins every team's). <paramref name="TeamId"/> is the clinic picker (MC6): only that team's
/// (none: all the viewer may see); anyone but an admin may pick only one of their own (400).
/// </para>
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
    bool? ClockStopped = null,
    Guid? TeamId = null,
    TeamScope? Scope = null)
    : IRequest<PagedResponse<ReferralSummaryResponse>>
{
    /// <summary>The viewer's team walls (the system's outside a request).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
