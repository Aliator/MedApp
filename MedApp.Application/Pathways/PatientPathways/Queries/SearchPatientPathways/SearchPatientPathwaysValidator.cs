using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;

public sealed class SearchPatientPathwaysValidator : AbstractValidator<SearchPatientPathwaysQuery>
{
    public SearchPatientPathwaysValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || PathwaySearch.IsSortColumn(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", PathwaySearch.SortColumns)}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");

        RuleFor(x => x.Title)
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.TemplateName)
            .MaximumLength(200)
            .WithMessage("Template name must not exceed 200 characters.");

        RuleFor(x => x.PatientName)
            .MaximumLength(200)
            .WithMessage("Patient name must not exceed 200 characters.");

        RuleForEach(x => x.Statuses)
            .Must(PathwayEnums.IsStatus)
            .WithMessage($"Each status must be one of: {PathwayEnums.StatusNames}.");

        RuleFor(x => x.Priority)
            .Must(PathwayEnums.IsPriority)
            .When(x => x.Priority is not null)
            .WithMessage($"Priority must be one of: {PathwayEnums.PriorityNames}.");

        RuleForEach(x => x.Priorities)
            .Must(PathwayEnums.IsPriority)
            .WithMessage($"Each priority must be one of: {PathwayEnums.PriorityNames}.");

        RuleFor(x => x.TeamIds)
            .Must(ids => ids is null || ids.Count <= PathwaySearch.MaxIdsPerFilter)
            .WithMessage($"Choose at most {PathwaySearch.MaxIdsPerFilter} teams.");

        RuleFor(x => x.StageDefinitionIds)
            .Must(ids => ids is null || ids.Count <= PathwaySearch.MaxIdsPerFilter)
            .WithMessage($"Choose at most {PathwaySearch.MaxIdsPerFilter} stages.");

        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom!.Value)
            .When(x => x.DueFrom.HasValue && x.DueTo.HasValue)
            .WithMessage("The end of the target date range must not be before its start.");
    }
}
