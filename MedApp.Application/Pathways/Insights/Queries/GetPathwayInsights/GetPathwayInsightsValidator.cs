using FluentValidation;

namespace MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;

public sealed class GetPathwayInsightsValidator : AbstractValidator<GetPathwayInsightsQuery>
{
    public GetPathwayInsightsValidator()
    {
        RuleFor(x => x.Weeks)
            .InclusiveBetween(1, GetPathwayInsightsQuery.MaxWeeks)
            .WithMessage($"Weeks must be between 1 and {GetPathwayInsightsQuery.MaxWeeks}.");

        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("Template ID must not be empty.");

        RuleFor(x => x.SpecialtyId)
            .NotEqual(Guid.Empty)
            .WithMessage("Specialty ID must not be empty.");
    }
}
