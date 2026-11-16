using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Patients.Queries.SearchRetentionReview;

public sealed class SearchRetentionReviewValidator : AbstractValidator<SearchRetentionReviewQuery>
{
    public SearchRetentionReviewValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
    }
}
