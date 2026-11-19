using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;
using MedApp.Contracts.Pathways.Insights.Requests;
using MedApp.Contracts.Pathways.Insights.Responses;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.InsightsTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PathwayInsightsControllerTests
{
    [Test]
    public async Task GetPathwayInsights_SendsTheWeeksAndTemplateAndReturnsTheInsights()
    {
        var insights = new PathwayInsightsResponse { Weeks = 26 };
        var mediator = new RecordingMediator(_ => insights);
        using var cts = new CancellationTokenSource();

        var user = SignedIn("Admin");

        var result = await new PathwayInsightsController(mediator)
            .WithRequest(user)
            .GetPathwayInsights(new GetPathwayInsightsRequest { Weeks = 26, TemplateId = TemplateId, TeamId = PathwaysTestConstants.TeamId }, cts.Token);

        // With the caller's team walls, so only what their teams may see is counted.
        mediator.Request.Should().Be(new GetPathwayInsightsQuery(26, TemplateId, PathwaysTestConstants.TeamId, user.ToTeamScope()));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(insights);
    }

    [Test]
    public void Request_Defaults_ToTwelveWeeksOfEveryTemplate()
    {
        var request = new GetPathwayInsightsRequest();

        request.Weeks.Should().Be(GetPathwayInsightsQuery.DefaultWeeks);
        request.TemplateId.Should().BeNull();
        request.TeamId.Should().BeNull();
    }

    [Test]
    public void GetPathwayInsights_NeedsViewInsights()
    {
        // The API has no fallback policy: without this attribute, anyone could read the counts and usernames.
        var action = typeof(PathwayInsightsController).GetMethod(nameof(PathwayInsightsController.GetPathwayInsights))!;

        action.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Cast<RequirePermissionAttribute>()
            .Should().ContainSingle().Which.Permission.Should().Be(Permission.ViewInsights);
        action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
        typeof(PathwayInsightsController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
    }
}
