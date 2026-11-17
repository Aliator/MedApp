using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Procedures.NoteDeletionRequests.Queries.GetNoteDeletionRequest;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Requests;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class NoteDeletionRequestsControllerTests
{
    private static readonly ProcedureActor Actor = new(UserId, Username);
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

    [TestCase(true, "Synthetic reason.")]
    [TestCase(false, null)]
    public async Task DecideNoteDeletionRequest_SendsTheDecisionAsTheUser(bool deleteNote, string? reason)
    {
        var request = new DecideNoteDeletionRequestRequest { DeleteNote = deleteNote, Reason = reason };

        var result = await Controller.DecideNoteDeletionRequest(RecordId, request, _cts.Token);

        _mediator.Request.Should().Be(new DecideNoteDeletionRequestCommand(RecordId, deleteNote, reason, Actor));
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
