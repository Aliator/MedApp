using FluentValidation;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;

public sealed class SearchProcedureAuditValidator : AbstractValidator<SearchProcedureAuditQuery>
{
    public SearchProcedureAuditValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Action)
            .Must(ProcedureEnums.IsAuditAction)
            .When(x => x.Action is not null)
            .WithMessage($"Action must be one of: {ProcedureEnums.AuditActionNames}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("The end of the date range must not be before its start.");
    }
}
