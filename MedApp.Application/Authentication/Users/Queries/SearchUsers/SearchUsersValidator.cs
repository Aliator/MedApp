using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Authentication.Users.Queries.SearchUsers;

public sealed class SearchUsersValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || string.Equals(sort, SearchUsersQuery.SortByUsername, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Sort must be {SearchUsersQuery.SortByUsername}.");

        RuleFor(x => x.Username).MaximumLength(256).WithMessage("Username must not exceed 256 characters.");
        RuleFor(x => x.Role).MaximumLength(256).WithMessage("Role must not exceed 256 characters.");
    }
}
