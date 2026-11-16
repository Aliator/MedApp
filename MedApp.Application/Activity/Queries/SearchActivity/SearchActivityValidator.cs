using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Activity.Queries.SearchActivity;

public sealed class SearchActivityValidator : AbstractValidator<SearchActivityQuery>
{
    public SearchActivityValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleForEach(x => x.Actions)
            .Must(ActivitySearch.IsAction)
            .WithMessage($"Each action must be one of: {ActivitySearch.ActionNames}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("The end of the date range must not be before its start.");
    }
}
