using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Contracts.Activity.Requests;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class ActivityControllerTests
{
    [Test]
    public async Task SearchActivity_SendsEveryFilterAndReturnsThePage()
    {
        var request = AutoFixtureFactory.Create().Create<SearchActivityRequest>();
        var page = new PagedResponse<ActivityEntryResponse>([], 1, 10, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await new ActivityController(mediator).WithRequest(SignedIn("Admin")).SearchActivity(request, cts.Token);

        mediator.Request.Should().Be(new SearchActivityQuery(
            request.Page, request.PageSize, request.Ascending, request.PatientId, request.UserId, request.Search,
            request.From, request.To, request.Actions));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }
}
