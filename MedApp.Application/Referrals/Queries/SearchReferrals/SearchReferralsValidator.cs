using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Referrals.Queries.SearchReferrals;

public sealed class SearchReferralsValidator : AbstractValidator<SearchReferralsQuery>
{
    public SearchReferralsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ReferralSearch.IsSortColumn(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", ReferralSearch.SortColumns)}.");

        RuleForEach(x => x.Statuses)
            .Must(ReferralSearch.IsStatus)
            .WithMessage($"Each status must be one of: {ReferralSearch.StatusNames}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");
    }
}
