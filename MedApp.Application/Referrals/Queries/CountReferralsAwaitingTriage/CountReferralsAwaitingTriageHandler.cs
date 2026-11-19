using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;

/// <summary>
/// The side nav's count of referrals awaiting triage: only those the viewer may see (the team walls: their teams', or
/// every team's for an admin; none for someone in no team), counted in SQL. <c>InYourTeams</c> says it's their teams'
/// (not an admin's every team). A count names no patients, so it records nothing in the activity log.
/// </summary>
public sealed class CountReferralsAwaitingTriageHandler(IReferralRepository repository)
    : IRequestHandler<CountReferralsAwaitingTriageQuery, ReferralCountResponse>
{
    public async Task<ReferralCountResponse> Handle(CountReferralsAwaitingTriageQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Teams;
        var count = await repository.CountAwaitingTriageAsync(scope, cancellationToken);
        return new ReferralCountResponse { Count = count, InYourTeams = !scope.SeesAllTeams && scope.TeamIds.Count > 0 };
    }
}
