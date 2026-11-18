using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;

/// <summary>
/// The side nav's count of referrals awaiting triage: in the viewer's teams, or every team's when they
/// belong to none. A count names no patients, so it records nothing in the activity log.
/// </summary>
public sealed class CountReferralsAwaitingTriageHandler(IReferralRepository repository, ITeamRepository teams)
    : IRequestHandler<CountReferralsAwaitingTriageQuery, ReferralCountResponse>
{
    public async Task<ReferralCountResponse> Handle(CountReferralsAwaitingTriageQuery request, CancellationToken cancellationToken)
    {
        var mine = (await teams.GetForUserAsync(request.ViewerId, cancellationToken)).Select(s => s.Id).ToList();
        var count = await repository.CountAwaitingTriageAsync(mine.Count > 0 ? mine : null, cancellationToken);
        return new ReferralCountResponse { Count = count, InYourTeams = mine.Count > 0 };
    }
}
