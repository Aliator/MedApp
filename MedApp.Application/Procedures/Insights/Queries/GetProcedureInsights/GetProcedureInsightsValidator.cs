using FluentValidation;

namespace MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;

public sealed class GetProcedureInsightsValidator : AbstractValidator<GetProcedureInsightsQuery>
{
    public GetProcedureInsightsValidator()
    {
        RuleFor(x => x.Weeks)
            .InclusiveBetween(1, GetProcedureInsightsQuery.MaxWeeks)
            .WithMessage($"Weeks must be between 1 and {GetProcedureInsightsQuery.MaxWeeks}.");

        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("Template ID must not be empty.");
    }
}
