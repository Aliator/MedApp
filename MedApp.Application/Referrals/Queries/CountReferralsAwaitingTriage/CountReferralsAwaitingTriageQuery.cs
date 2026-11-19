using MedApp.Application.Common.Teams;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;

/// <summary>
/// How many referrals await triage that the viewer may see (<paramref name="Scope"/>: their teams', or every team's
/// for an admin).
/// </summary>
public sealed record CountReferralsAwaitingTriageQuery(Guid ViewerId, TeamScope? Scope = null) : IRequest<ReferralCountResponse>
{
    /// <summary>The viewer's team walls (the system's outside a request).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
