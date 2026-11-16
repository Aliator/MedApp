using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Demographics.Queries.GetDemographicsStatus;
using MedApp.Application.Demographics.Queries.LookUpDemographics;
using MedApp.Contracts.Demographics.Requests;
using MedApp.Contracts.Demographics.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class DemographicsControllerTests
{
    [Test]
    public async Task GetStatus_ReturnsIt()
    {
        var status = new DemographicsStatusResponse { IsAvailable = true, Source = "Stub" };
        var mediator = new RecordingMediator(_ => status);

        var result = await new DemographicsController(mediator).WithRequest(SignedIn("User")).GetStatus(CancellationToken.None);

        mediator.Request.Should().Be(new GetDemographicsStatusQuery());
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(status);
    }

    [Test]
    public async Task LookUp_NoNumber_SendsAnEmptyOne()
    {
        var mediator = new RecordingMediator(_ => new DemographicsLookupResponse());

        await new DemographicsController(mediator).WithRequest(SignedIn("User")).LookUp(new LookUpDemographicsRequest(), CancellationToken.None);

        mediator.Request.Should().Be(new LookUpDemographicsQuery(""));
    }

    [Test]
    public async Task LookUp_SendsTheNumber_ReturnsThePersonUncached()
    {
        var person = new DemographicsLookupResponse { NhsNumber = NhsNumber, FirstName = "Ada", LastName = "Synthetic" };
        var mediator = new RecordingMediator(_ => person);
        using var cts = new CancellationTokenSource();
        var controller = new DemographicsController(mediator).WithRequest(SignedIn("User"));

        var result = await controller.LookUp(new LookUpDemographicsRequest { NhsNumber = NhsNumberWithSpaces }, cts.Token);

        mediator.Request.Should().Be(new LookUpDemographicsQuery(NhsNumberWithSpaces));
        mediator.Tokens.Should().Equal(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(person);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }
}
