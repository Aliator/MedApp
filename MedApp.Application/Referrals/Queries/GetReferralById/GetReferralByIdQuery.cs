using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.GetReferralById;

/// <summary>One referral, with its history and what <paramref name="Viewer"/> may do with it.</summary>
public sealed record GetReferralByIdQuery(Guid ReferralId, PathwayActor Viewer) : IRequest<ReferralResponse>;
