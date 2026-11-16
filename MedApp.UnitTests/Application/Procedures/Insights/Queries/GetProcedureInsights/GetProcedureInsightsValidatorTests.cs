using FluentValidation.TestHelper;
using MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Insights.Queries.GetProcedureInsights;

[TestFixture]
public sealed class GetProcedureInsightsValidatorTests
{
    private readonly GetProcedureInsightsValidator _validator = new();

    [TestCase(1)]
    [TestCase(GetProcedureInsightsQuery.DefaultWeeks)]
    [TestCase(GetProcedureInsightsQuery.MaxWeeks)]
    public void Validate_WeeksInRange_HasNoErrors(int weeks)
    {
        _validator.TestValidate(new GetProcedureInsightsQuery(weeks, TemplateId)).ShouldNotHaveAnyValidationErrors();
    }

    [TestCase(0)]
    [TestCase(-4)]
    [TestCase(GetProcedureInsightsQuery.MaxWeeks + 1)]
    public void Validate_WeeksOutOfRange_HasError(int weeks)
    {
        _validator.TestValidate(new GetProcedureInsightsQuery(weeks, null))
            .ShouldHaveValidationErrorFor(x => x.Weeks)
            .WithErrorMessage("Weeks must be between 1 and 52.");
    }

    [Test]
    public void Validate_NoTemplate_HasNoErrors()
    {
        _validator.TestValidate(new GetProcedureInsightsQuery(4, null)).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Validate_EmptyTemplateId_HasError()
    {
        _validator.TestValidate(new GetProcedureInsightsQuery(4, Guid.Empty)).ShouldHaveValidationErrorFor(x => x.TemplateId);
    }
}
