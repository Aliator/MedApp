using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

/// <summary>
/// One page of referrals, filtered and sorted (see <see cref="ReferralSearch"/>). <paramref name="Mine"/> limits it to
/// the specialties <paramref name="ViewerId"/> belongs to (every specialty, if they belong to none).
/// </summary>
public sealed record SearchReferralsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    IReadOnlyList<string>? Statuses,
    IReadOnlyList<Guid>? SpecialtyIds,
    bool Mine,
    Guid? PatientId,
    string? Search,
    Guid ViewerId)
    : IRequest<PagedResponse<ReferralSummaryResponse>>;
