using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.CountReferralsAwaitingTriage;

/// <summary>How many referrals await triage in <paramref name="ViewerId"/>'s teams (every one's, if they belong to none).</summary>
public sealed record CountReferralsAwaitingTriageQuery(Guid ViewerId) : IRequest<ReferralCountResponse>;
