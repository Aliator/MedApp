using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;

public sealed class SearchPatientProceduresValidator : AbstractValidator<SearchPatientProceduresQuery>
{
    public SearchPatientProceduresValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || ProcedureSearch.IsSortColumn(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", ProcedureSearch.SortColumns)}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");

        RuleForEach(x => x.Statuses)
            .Must(ProcedureEnums.IsStatus)
            .WithMessage($"Each status must be one of: {ProcedureEnums.StatusNames}.");

        RuleFor(x => x.Priority)
            .Must(ProcedureEnums.IsPriority)
            .When(x => x.Priority is not null)
            .WithMessage($"Priority must be one of: {ProcedureEnums.PriorityNames}.");

        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom!.Value)
            .When(x => x.DueFrom.HasValue && x.DueTo.HasValue)
            .WithMessage("The end of the due date range must not be before its start.");
    }
}
