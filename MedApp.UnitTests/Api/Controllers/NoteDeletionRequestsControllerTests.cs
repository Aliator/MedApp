using MedApp.UnitTests.Common.Constants;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class NoteDeletionRequestsControllerTests
{
    // Signed in as an admin, so the actor says so.
    private static readonly PathwayActor Actor = new(UserId, Username, TestPermissions.Admin);
    private NoteDeletionRequestResponse _response = null!;
    private RecordingMediator _mediator = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _response = AutoFixtureFactory.Create().Create<NoteDeletionRequestResponse>();
        _mediator = new RecordingMediator(_ => _response);
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private NoteDeletionRequestsController Controller => new NoteDeletionRequestsController(_mediator).WithRequest(SignedIn("Admin"));

    [Test]
    public async Task GetNoteDeletionRequest_ReturnsIt()
    {
        var result = await Controller.GetNoteDeletionRequest(RecordId, _cts.Token);

        _mediator.Request.Should().Be(new GetNoteDeletionRequestQuery(RecordId));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(_response);
    }

    [TestCase(true, 2, "WrittenInError", "Synthetic detail.")]
    [TestCase(false, null, null, null)]
    public async Task DecideNoteDeletionRequest_SendsTheDecisionAsTheUser(bool deleteNote, int? version, string? reason, string? detail)
    {
        var request = new DecideNoteDeletionRequestRequest { DeleteNote = deleteNote, ExpectedVersion = version, Reason = reason, Detail = detail };

        var result = await Controller.DecideNoteDeletionRequest(RecordId, request, _cts.Token);

        _mediator.Request.Should().Be(new DecideNoteDeletionRequestCommand(RecordId, deleteNote, version, reason, detail, Actor));
        _mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(_response);
    }

    [Test]
    public async Task EveryAction_AdminsOnly()
    {
        var actions = ControllerAuthorisation.Actions<NoteDeletionRequestsController>().ToList();

        actions.Should().HaveCount(2);
        foreach (var action in actions)
        {
            (await ControllerAuthorisation.MayCallAsync<NoteDeletionRequestsController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<NoteDeletionRequestsController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<NoteDeletionRequestsController>(action, SignedIn())).Should().BeFalse(action);
        }
    }
}
