using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

/// <summary>A page of referrals. Each patient on the page is recorded as listed (once per person every few minutes).</summary>
public sealed class SearchReferralsHandler(
    IReferralRepository repository,
    ISpecialtyRepository specialties,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchReferralsQuery, PagedResponse<ReferralSummaryResponse>>
{
    public async Task<PagedResponse<ReferralSummaryResponse>> Handle(SearchReferralsQuery request, CancellationToken cancellationToken)
    {
        var specialtyIds = await SpecialtiesToShowAsync(request, cancellationToken);
        var result = await repository.SearchAsync(request, specialtyIds, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(row => row.Referral.PatientId), cancellationToken);

        var today = DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime);
        return PagedMapper.ToResponse(result, request.Page, request.PageSize, row => ReferralMapper.ToSummary(row, today));
    }

    /// <summary>
    /// The specialties to keep: those asked for, and with "mine" only those of them the viewer belongs to (all the
    /// viewer's when none are asked for); null for every specialty (nothing asked, or "mine" from someone in none).
    /// </summary>
    private async Task<IReadOnlyCollection<Guid>?> SpecialtiesToShowAsync(SearchReferralsQuery request, CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? asked = request.SpecialtyIds is { Count: > 0 } ids ? ids.ToHashSet() : null;
        if (!request.Mine)
            return asked;

        var mine = (await specialties.GetForUserAsync(request.ViewerId, ct)).Select(s => s.Id).ToHashSet();
        if (mine.Count == 0)
            return asked;

        return asked is null ? mine : asked.Where(mine.Contains).ToList();
    }
}
