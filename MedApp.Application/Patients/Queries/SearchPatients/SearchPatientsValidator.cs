using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Patients.Queries.SearchPatients;

public sealed class SearchPatientsValidator : AbstractValidator<SearchPatientsQuery>
{
    public SearchPatientsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || PatientSearch.IsSortColumn(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", PatientSearch.SortColumns)}.");

        RuleFor(x => x.FirstName).MaximumLength(100).WithMessage("First name must not exceed 100 characters.");
        RuleFor(x => x.LastName).MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");
        RuleFor(x => x.Email).MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.DateOfBirthTo)
            .GreaterThanOrEqualTo(x => x.DateOfBirthFrom!.Value)
            .When(x => x.DateOfBirthFrom.HasValue && x.DateOfBirthTo.HasValue)
            .WithMessage("The end of the date of birth range must not be before its start.");
    }
}
