using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;

public sealed class SearchPathwayAuditValidator : AbstractValidator<SearchPathwayAuditQuery>
{
    public SearchPathwayAuditValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Action)
            .Must(PathwayEnums.IsAuditAction)
            .When(x => x.Action is not null)
            .WithMessage($"Action must be one of: {PathwayEnums.AuditActionNames}.");

        RuleForEach(x => x.Actions)
            .Must(PathwayEnums.IsAuditAction)
            .WithMessage($"Each action must be one of: {PathwayEnums.AuditActionNames}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("The end of the date range must not be before its start.");
    }
}
