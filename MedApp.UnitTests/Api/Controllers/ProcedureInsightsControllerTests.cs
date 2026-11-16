using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;
using MedApp.Contracts.Procedures.Insights.Requests;
using MedApp.Contracts.Procedures.Insights.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class ProcedureInsightsControllerTests
{
    [Test]
    public async Task GetProcedureInsights_SendsTheWeeksAndTemplateAndReturnsTheInsights()
    {
        var insights = new ProcedureInsightsResponse { Weeks = 26 };
        var mediator = new RecordingMediator(_ => insights);
        using var cts = new CancellationTokenSource();

        var result = await new ProcedureInsightsController(mediator)
            .WithRequest(SignedIn("Admin"))
            .GetProcedureInsights(new GetProcedureInsightsRequest { Weeks = 26, TemplateId = TemplateId }, cts.Token);

        mediator.Request.Should().Be(new GetProcedureInsightsQuery(26, TemplateId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(insights);
    }

    [Test]
    public void Request_Defaults_ToTwelveWeeksOfEveryTemplate()
    {
        var request = new GetProcedureInsightsRequest();

        request.Weeks.Should().Be(GetProcedureInsightsQuery.DefaultWeeks);
        request.TemplateId.Should().BeNull();
    }

    [Test]
    public void GetProcedureInsights_IsForAdminsOnly()
    {
        // The API has no fallback policy: without this attribute, anyone could read the counts and usernames.
        var action = typeof(ProcedureInsightsController).GetMethod(nameof(ProcedureInsightsController.GetProcedureInsights))!;

        action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle().Which.Roles.Should().Be("Admin");
        action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
        typeof(ProcedureInsightsController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
    }
}
