using FluentValidation.TestHelper;
using MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Insights.Queries.GetPathwayInsights;

[TestFixture]
public sealed class GetPathwayInsightsValidatorTests
{
    private readonly GetPathwayInsightsValidator _validator = new();

    [TestCase(1)]
    [TestCase(GetPathwayInsightsQuery.DefaultWeeks)]
    [TestCase(GetPathwayInsightsQuery.MaxWeeks)]
    public void Validate_WeeksInRange_HasNoErrors(int weeks)
    {
        _validator.TestValidate(new GetPathwayInsightsQuery(weeks, TemplateId)).ShouldNotHaveAnyValidationErrors();
    }

    [TestCase(0)]
    [TestCase(-4)]
    [TestCase(GetPathwayInsightsQuery.MaxWeeks + 1)]
    public void Validate_WeeksOutOfRange_HasError(int weeks)
    {
        _validator.TestValidate(new GetPathwayInsightsQuery(weeks, null))
            .ShouldHaveValidationErrorFor(x => x.Weeks)
            .WithErrorMessage("Weeks must be between 1 and 52.");
    }

    [Test]
    public void Validate_NoTemplate_HasNoErrors()
    {
        _validator.TestValidate(new GetPathwayInsightsQuery(4, null)).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_EmptyTemplateId_HasError()
    {
        _validator.TestValidate(new GetPathwayInsightsQuery(4, Guid.Empty)).ShouldHaveValidationErrorFor(x => x.TemplateId);
    }

    [Test]
    public void Validate_EmptySpecialtyId_HasError()
    {
        _validator.TestValidate(new GetPathwayInsightsQuery(4, null, Guid.Empty)).ShouldHaveValidationErrorFor(x => x.SpecialtyId);
    }
}
