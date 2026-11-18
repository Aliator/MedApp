using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Contracts.Pathways.Timeline.Requests;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientTimelineControllerTests
{
    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task GetTimeline_SendsEveryFilterAndWhetherTheyAreAnAdmin(string role, bool isAdmin)
    {
        var request = new GetPatientTimelineRequest { Before = "cursor", PageSize = 5, Kinds = ["Notes"], PathwayIds = [OtherId] };
        var page = new PatientTimelinePageResponse();
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await new PatientTimelineController(mediator).WithRequest(SignedIn(role)).GetTimeline(RecordId, request, cts.Token);

        mediator.Request.Should().Be(new GetPatientTimelineQuery(RecordId, "cursor", 5, request.Kinds, request.PathwayIds, isAdmin));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [TestCase("User", false)]
    [TestCase("Admin", true)]
    public async Task GetOverview_SendsThePatientAndWhetherTheyAreAnAdmin(string role, bool isAdmin)
    {
        var overview = new PatientTimelineOverviewResponse();
        var mediator = new RecordingMediator(_ => overview);
        using var cts = new CancellationTokenSource();

        var result = await new PatientTimelineController(mediator).WithRequest(SignedIn(role)).GetOverview(RecordId, cts.Token);

        mediator.Request.Should().Be(new GetPatientTimelineOverviewQuery(RecordId, isAdmin));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(overview);
    }
}
